using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using NAudio.Wave;
using Vosk;

namespace Nova.Desktop;

internal static class VoskAudioTranscriber
{
    private const string ModelName = "vosk-model-small-de-0.15";
    private const string ModelUrl = "https://alphacephei.com/vosk/models/vosk-model-small-de-0.15.zip";
    private const int SampleRate = 16_000;
    private const int AudioBufferBytes = 32 * 1024;
    private const long MaxModelArchiveBytes = 80L * 1024 * 1024;
    private const long MaxExtractedModelBytes = 200L * 1024 * 1024;
    private const long MaxAudioFileBytes = 2L * 1024 * 1024 * 1024;
    private const int MaxTranscriptCharacters = 2_000_000;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };
    private static readonly string ModelDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NOVA",
        "speech-models",
        ModelName);

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav", ".mp3", ".m4a", ".aac", ".wma", ".flac"
    };

    public static bool IsSupportedAudioFile(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path));

    public static bool IsModelInstalled => IsModelValid(ModelDirectory);

    public static async Task<IReadOnlyDictionary<string, string>> TranscribeFilesAsync(
        IReadOnlyList<string> audioPaths,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        if (audioPaths.Count == 0)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var modelPath = await EnsureModelInstalledAsync(progress, cancellationToken);
        return await Task.Run(
            () => TranscribeFiles(modelPath, audioPaths, progress, cancellationToken),
            cancellationToken);
    }

    private static async Task<string> EnsureModelInstalledAsync(
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        if (Directory.Exists(ModelDirectory))
        {
            if (IsModelValid(ModelDirectory))
                return ModelDirectory;
            throw new InvalidDataException(
                $"Das installierte Vosk-Modell ist unvollständig. Lösche den Ordner „{ModelDirectory}“ und versuche es erneut.");
        }

        var modelRoot = Path.GetDirectoryName(ModelDirectory)!;
        Directory.CreateDirectory(modelRoot);
        var suffix = Guid.NewGuid().ToString("N");
        var archivePath = Path.Combine(modelRoot, $"{ModelName}-{suffix}.zip.part");
        var stagingPath = Path.Combine(modelRoot, $"{ModelName}-{suffix}.extracting");
        try
        {
            progress.Report("Das deutsche Vosk-Modell (ca. 45 MB) wird einmalig heruntergeladen …");
            await DownloadModelAsync(archivePath, progress, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            progress.Report("Vosk-Modell wird geprüft und lokal installiert …");
            ExtractModel(archivePath, stagingPath, cancellationToken);
            var extractedModelPath = Path.Combine(stagingPath, ModelName);
            if (!IsModelValid(extractedModelPath))
                throw new InvalidDataException("Das heruntergeladene Vosk-Modell enthält nicht die erwarteten Modelldateien.");
            Directory.Move(extractedModelPath, ModelDirectory);
            progress.Report("Deutsches Offline-Sprachmodell ist bereit.");
            return ModelDirectory;
        }
        finally
        {
            if (File.Exists(archivePath))
                File.Delete(archivePath);
            if (Directory.Exists(stagingPath))
                Directory.Delete(stagingPath, recursive: true);
        }
    }

    private static async Task DownloadModelAsync(
        string archivePath,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        using var response = await Http.GetAsync(
            ModelUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > MaxModelArchiveBytes)
            throw new InvalidDataException("Das Vosk-Modellarchiv ist größer als das erlaubte Downloadlimit.");

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = new FileStream(
            archivePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            AudioBufferBytes,
            useAsync: true);
        var buffer = new byte[AudioBufferBytes];
        long downloadedBytes = 0;
        var nextProgressAt = 0L;
        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            downloadedBytes += bytesRead;
            if (downloadedBytes > MaxModelArchiveBytes)
                throw new InvalidDataException("Das Vosk-Modellarchiv überschreitet das erlaubte Downloadlimit.");
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            if (downloadedBytes >= nextProgressAt)
            {
                var totalBytes = response.Content.Headers.ContentLength;
                var status = totalBytes is > 0
                    ? $"Vosk-Modell wird heruntergeladen: {downloadedBytes * 100 / totalBytes.Value} %"
                    : $"Vosk-Modell wird heruntergeladen: {downloadedBytes / (1024 * 1024)} MB";
                progress.Report(status);
                nextProgressAt = downloadedBytes + 2L * 1024 * 1024;
            }
        }
        await destination.FlushAsync(cancellationToken);
        if (downloadedBytes == 0)
            throw new InvalidDataException("Der Vosk-Modellserver hat eine leere Datei zurückgegeben.");
    }

    private static void ExtractModel(
        string archivePath,
        string stagingPath,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(stagingPath);
        var destinationRoot = Path.GetFullPath(stagingPath) + Path.DirectorySeparatorChar;
        long expandedBytes = 0;
        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destinationPath = Path.GetFullPath(Path.Combine(stagingPath, entry.FullName));
            if (!destinationPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Das Vosk-Archiv enthält einen ungültigen Dateipfad.");
            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            using var source = entry.Open();
            using var destination = new FileStream(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                AudioBufferBytes);
            var buffer = new byte[AudioBufferBytes];
            long entryBytes = 0;
            int bytesRead;
            while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                expandedBytes = checked(expandedBytes + bytesRead);
                entryBytes += bytesRead;
                if (expandedBytes > MaxExtractedModelBytes)
                    throw new InvalidDataException("Das entpackte Vosk-Modell überschreitet das erlaubte Größenlimit.");
                destination.Write(buffer, 0, bytesRead);
            }
            if (entryBytes != entry.Length)
                throw new InvalidDataException($"Die Modelldatei „{entry.Name}“ wurde unvollständig entpackt.");
        }
    }

    private static IReadOnlyDictionary<string, string> TranscribeFiles(
        string modelPath,
        IReadOnlyList<string> audioPaths,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        var transcripts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var model = new Model(modelPath);
        for (var fileIndex = 0; fileIndex < audioPaths.Count; fileIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = audioPaths[fileIndex];
            var info = new FileInfo(path);
            if (!info.Exists)
                throw new FileNotFoundException($"Die ausgewählte Audiodatei existiert nicht mehr: {info.Name}", path);
            if (info.Length > MaxAudioFileBytes)
                throw new InvalidDataException($"„{info.Name}“ ist größer als 2 GB.");

            progress.Report($"Vosk transkribiert Audiodatei {fileIndex + 1}/{audioPaths.Count}: {info.Name}");
            using var reader = new MediaFoundationReader(path);
            using var resampler = new MediaFoundationResampler(reader, new WaveFormat(SampleRate, 16, 1))
            {
                ResamplerQuality = 60
            };
            using var recognizer = new VoskRecognizer(model, SampleRate);
            var transcript = new StringBuilder();
            var buffer = new byte[AudioBufferBytes];
            var lastProgressAt = Environment.TickCount64;
            int bytesRead;
            while ((bytesRead = resampler.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (recognizer.AcceptWaveform(buffer, bytesRead))
                    AppendRecognizedText(transcript, recognizer.Result());

                var now = Environment.TickCount64;
                if (now - lastProgressAt >= 1_000)
                {
                    var duration = reader.TotalTime.TotalSeconds;
                    var percent = duration > 0
                        ? Math.Clamp((int)(reader.CurrentTime.TotalSeconds * 100 / duration), 0, 99)
                        : 0;
                    progress.Report($"Vosk transkribiert {info.Name}: {percent} %");
                    lastProgressAt = now;
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            AppendRecognizedText(transcript, recognizer.FinalResult());
            var result = transcript.ToString().Trim();
            if (result.Length > MaxTranscriptCharacters)
                throw new InvalidDataException($"Das Transkript von „{info.Name}“ überschreitet das 2-Mio.-Zeichen-Limit.");
            if (result.Length == 0)
                throw new InvalidDataException($"In „{info.Name}“ wurde keine Sprache erkannt.");
            transcripts.Add(path, result);
            progress.Report($"Audiodatei {fileIndex + 1}/{audioPaths.Count} transkribiert.");
        }
        return transcripts;
    }

    private static void AppendRecognizedText(StringBuilder transcript, string resultJson)
    {
        using var result = JsonDocument.Parse(resultJson);
        if (result.RootElement.TryGetProperty("text", out var textElement))
        {
            var text = textElement.GetString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                if (transcript.Length > 0)
                    transcript.Append(' ');
                transcript.Append(text);
            }
        }
        if (transcript.Length > MaxTranscriptCharacters)
            throw new InvalidDataException("Das erkannte Transkript überschreitet das 2-Mio.-Zeichen-Limit.");
    }

    private static bool IsModelValid(string path) =>
        File.Exists(Path.Combine(path, "am", "final.mdl"))
        && File.Exists(Path.Combine(path, "conf", "model.conf"));
}
