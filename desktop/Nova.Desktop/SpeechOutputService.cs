using System.Speech.Synthesis;
using System.Collections.Concurrent;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Nova.Desktop;

internal sealed class SpeechOutputService : IDisposable
{
    private readonly BlockingCollection<SpeechRequest> _requests = new();
    private readonly Thread _worker;
    private readonly object _outputLock = new();
    private WasapiOut? _activeOutput;
    private SpeechConfiguration _configuration = new("", 2, 100, "");
    private int _generation;
    private int _pendingCount;
    private bool _disposed;

    public SpeechOutputService()
    {
        _worker = new Thread(ProcessQueue)
        {
            IsBackground = true,
            Name = "NOVA speech output"
        };
        _worker.SetApartmentState(ApartmentState.STA);
        _worker.Start();
    }

    public bool IsSpeaking => Volatile.Read(ref _pendingCount) > 0;
    public event Action<string>? Error;
    public event Action<bool>? SpeakingChanged;

    public void Configure(UserSettings settings)
    {
        Volatile.Write(ref _configuration, new SpeechConfiguration(
            settings.SpeechVoice,
            Math.Clamp(settings.SpeechRate, -10, 10),
            Math.Clamp(settings.SpeechVolume, 0, 100),
            settings.SpeakerDeviceId));
    }

    public void Speak(string text)
    {
        if (_disposed || string.IsNullOrWhiteSpace(text))
            return;
        var request = new SpeechRequest(
            text,
            Volatile.Read(ref _generation),
            Volatile.Read(ref _configuration));
        if (Interlocked.Increment(ref _pendingCount) == 1)
            SpeakingChanged?.Invoke(true);
        try
        {
            _requests.Add(request);
        }
        catch (InvalidOperationException)
        {
            if (Interlocked.Decrement(ref _pendingCount) == 0)
                SpeakingChanged?.Invoke(false);
            Error?.Invoke("Die Sprachausgabe-Warteschlange ist geschlossen.");
        }
    }

    public void CancelAll()
    {
        Interlocked.Increment(ref _generation);
        lock (_outputLock)
            _activeOutput?.Stop();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        CancelAll();
        _disposed = true;
        _requests.CompleteAdding();
        _worker.Join();
        _requests.Dispose();
    }

    private void ProcessQueue()
    {
        using var synthesizer = new SpeechSynthesizer();
        foreach (var request in _requests.GetConsumingEnumerable())
        {
            try
            {
                if (request.Generation == Volatile.Read(ref _generation))
                    SpeakRequest(request, synthesizer);
            }
            catch (Exception error) when (error is IOException
                                          or InvalidOperationException
                                          or ArgumentException
                                          or System.Runtime.InteropServices.COMException
                                          or PlatformNotSupportedException)
            {
                Error?.Invoke($"Sprachausgabe fehlgeschlagen: {error.Message}");
            }
            finally
            {
                if (Interlocked.Decrement(ref _pendingCount) == 0)
                    SpeakingChanged?.Invoke(false);
            }
        }
    }

    private void SpeakRequest(SpeechRequest request, SpeechSynthesizer synthesizer)
    {
        synthesizer.Rate = request.Configuration.Rate;
        if (!string.IsNullOrWhiteSpace(request.Configuration.Voice))
            synthesizer.SelectVoice(request.Configuration.Voice);
        else
        {
            var germanVoice = synthesizer.GetInstalledVoices()
                .FirstOrDefault(voice => voice.Enabled
                    && voice.VoiceInfo.Culture.Name.StartsWith("de", StringComparison.OrdinalIgnoreCase));
            if (germanVoice is not null)
                synthesizer.SelectVoice(germanVoice.VoiceInfo.Name);
            else
            {
                try
                {
                    synthesizer.SelectVoiceByHints(
                        VoiceGender.NotSet,
                        VoiceAge.NotSet,
                        0,
                        System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
                }
                catch (InvalidOperationException)
                {
                    Error?.Invoke("Keine deutsche Windows-Stimme installiert. NOVA verwendet die Systemstimme; installiere ein deutsches Sprachpaket für natürliche Aussprache.");
                }
            }
        }
        using var waveStream = new MemoryStream();
        synthesizer.SetOutputToWaveStream(waveStream);
        synthesizer.Speak(request.Text);
        synthesizer.SetOutputToNull();
        if (request.Generation != Volatile.Read(ref _generation) || waveStream.Length == 0)
            return;

        waveStream.Position = 0;
        using var reader = new WaveFileReader(waveStream);
        using var enumerator = new MMDeviceEnumerator();
        using var device = string.IsNullOrWhiteSpace(request.Configuration.DeviceId)
            ? enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
            : enumerator.GetDevice(request.Configuration.DeviceId);
        using var output = new WasapiOut(device, AudioClientShareMode.Shared, true, 80)
        {
            Volume = request.Configuration.Volume / 100f
        };
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        output.PlaybackStopped += (_, args) =>
        {
            if (args.Exception is not null)
                completion.TrySetException(args.Exception);
            else
                completion.TrySetResult();
        };
        lock (_outputLock)
        {
            if (request.Generation != Volatile.Read(ref _generation))
                return;
            _activeOutput = output;
            output.Init(reader);
            output.Play();
        }
        completion.Task.GetAwaiter().GetResult();
        lock (_outputLock)
            if (ReferenceEquals(_activeOutput, output))
                _activeOutput = null;
    }

    private sealed record SpeechRequest(string Text, int Generation, SpeechConfiguration Configuration);
    private sealed record SpeechConfiguration(string Voice, int Rate, int Volume, string DeviceId);
}
