using System.Collections.Concurrent;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Speech.AudioFormat;

namespace Nova.Desktop;

internal sealed class MicrophoneCaptureSession : IDisposable
{
    public static readonly SpeechAudioFormatInfo SpeechFormat =
        new(16000, AudioBitsPerSample.Sixteen, AudioChannel.Mono);

    private readonly MMDevice _device;
    private readonly WasapiCapture _capture;
    private readonly AudioPipeStream _audioStream = new();
    private readonly Pcm16kMonoConverter _converter;

    public MicrophoneCaptureSession(string deviceId, int volume)
    {
        using var enumerator = new MMDeviceEnumerator();
        _device = enumerator.GetDevice(deviceId);
        _capture = new WasapiCapture(_device);
        _converter = new Pcm16kMonoConverter(_capture.WaveFormat, volume);
        _capture.DataAvailable += Capture_DataAvailable;
        _capture.RecordingStopped += Capture_RecordingStopped;
    }

    public Stream AudioStream => _audioStream;

    public void Start() => _capture.StartRecording();

    public void Dispose()
    {
        _capture.DataAvailable -= Capture_DataAvailable;
        _capture.RecordingStopped -= Capture_RecordingStopped;
        if (_capture.CaptureState != CaptureState.Stopped)
            _capture.StopRecording();
        _capture.Dispose();
        _audioStream.Dispose();
        _device.Dispose();
    }

    private void Capture_DataAvailable(object? sender, WaveInEventArgs args)
    {
        var converted = _converter.Convert(args.Buffer, args.BytesRecorded);
        if (converted.Length > 0)
            _audioStream.Write(converted, 0, converted.Length);
    }

    private void Capture_RecordingStopped(object? sender, StoppedEventArgs args) =>
        _audioStream.Complete();

    private sealed class Pcm16kMonoConverter
    {
        private const int OutputRate = 16000;
        private readonly WaveFormat _format;
        private readonly int _volume;
        private readonly int _frameBytes;
        private readonly int _bytesPerSample;
        private readonly bool _isFloat;
        private readonly double _sourceStep;
        private double _sourcePosition;
        private double _previousSample;
        private long _frameIndex;
        private bool _hasPrevious;

        public Pcm16kMonoConverter(WaveFormat format, int volume)
        {
            if (format.Channels < 1 || format.SampleRate < OutputRate
                || format.BitsPerSample is not (8 or 16 or 24 or 32))
                throw new NotSupportedException(
                    $"Das Mikrofonformat {format} kann nicht in Spracherkennung umgewandelt werden.");
            _format = format;
            _volume = Math.Clamp(volume, 0, 200);
            _bytesPerSample = format.BitsPerSample / 8;
            _frameBytes = _bytesPerSample * format.Channels;
            _isFloat = format.Encoding == WaveFormatEncoding.IeeeFloat
                || format.Encoding == WaveFormatEncoding.Extensible && format.BitsPerSample == 32;
            _sourceStep = (double)format.SampleRate / OutputRate;
        }

        public byte[] Convert(byte[] input, int byteCount)
        {
            var output = new List<byte>(byteCount);
            var frames = byteCount / _frameBytes;
            for (var frame = 0; frame < frames; frame++)
            {
                var frameOffset = frame * _frameBytes;
                var monoSample = 0d;
                for (var channel = 0; channel < _format.Channels; channel++)
                    monoSample += ReadSample(input, frameOffset + channel * _bytesPerSample);
                monoSample = Math.Clamp(monoSample / _format.Channels, -1d, 1d);

                if (!_hasPrevious)
                {
                    _previousSample = monoSample;
                    _hasPrevious = true;
                    WriteSample(output, monoSample);
                    _sourcePosition += _sourceStep;
                    _frameIndex++;
                    continue;
                }

                while (_sourcePosition <= _frameIndex)
                {
                    var fraction = _sourcePosition - (_frameIndex - 1);
                    WriteSample(output, _previousSample + (monoSample - _previousSample) * fraction);
                    _sourcePosition += _sourceStep;
                }
                _previousSample = monoSample;
                _frameIndex++;
            }
            return output.ToArray();
        }

        private double ReadSample(byte[] data, int offset)
        {
            if (_isFloat)
                return Math.Clamp(BitConverter.ToSingle(data, offset), -1f, 1f);
            return _format.BitsPerSample switch
            {
                8 => (data[offset] - 128) / 128d,
                16 => BitConverter.ToInt16(data, offset) / 32768d,
                24 => ReadInt24(data, offset) / 8388608d,
                32 => BitConverter.ToInt32(data, offset) / 2147483648d,
                _ => throw new NotSupportedException("Nicht unterstützte Mikrofon-Bittiefe.")
            };
        }

        private void WriteSample(List<byte> output, double sample)
        {
            var value = (short)Math.Clamp(
                Math.Round(sample * _volume / 100d * short.MaxValue),
                short.MinValue,
                short.MaxValue);
            output.Add((byte)(value & 0xff));
            output.Add((byte)((value >> 8) & 0xff));
        }

        private static int ReadInt24(byte[] data, int offset)
        {
            var value = data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16;
            return (value & 0x800000) != 0 ? value | unchecked((int)0xff000000) : value;
        }
    }

    private sealed class AudioPipeStream : Stream
    {
        private readonly BlockingCollection<byte[]> _chunks = new();
        private byte[]? _current;
        private int _offset;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count == 0)
                return 0;
            while (_current is null || _offset >= _current.Length)
            {
                if (!_chunks.TryTake(out _current, Timeout.Infinite))
                    return 0;
                _offset = 0;
            }
            var bytes = Math.Min(count, _current.Length - _offset);
            Array.Copy(_current, _offset, buffer, offset, bytes);
            _offset += bytes;
            return bytes;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (count == 0 || _chunks.IsAddingCompleted)
                return;
            var copy = new byte[count];
            Array.Copy(buffer, offset, copy, 0, count);
            try
            {
                _chunks.Add(copy);
            }
            catch (InvalidOperationException) when (_chunks.IsAddingCompleted)
            {
            }
        }

        public void Complete()
        {
            if (!_chunks.IsAddingCompleted)
                _chunks.CompleteAdding();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Complete();
                _chunks.Dispose();
            }
            base.Dispose(disposing);
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
