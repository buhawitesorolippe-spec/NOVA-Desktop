using System.Text.Json;
using System.Globalization;

namespace Nova.Desktop;

public sealed class UserSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NOVA",
        "settings.json");

    public string Provider { get; set; } = "local";
    public string Theme { get; set; } = "Dark";
    public Dictionary<string, string> CloudModels { get; set; } = [];
    public List<string> AdditionalModelDirectories { get; set; } = [];
    public string ModelFile { get; set; } = "";
    public string ResearchModelFile { get; set; } = "";
    public string ResearchInstruction { get; set; } = "";
    public string CoderModelFile { get; set; } = "";
    public string CoderProjectDirectory { get; set; } = "";
    public string LlamaServerPath { get; set; } = "llama-server";
    public string VisionProjectorPath { get; set; } = "";
    public string SystemPrompt { get; set; } = "";
    public int ContextLength { get; set; } = 4096;
    public int GpuLayers { get; set; } = 99;
    public int CpuThreads { get; set; } = Math.Max(1, Environment.ProcessorCount - 1);
    public int BatchSize { get; set; } = 512;
    public int PhysicalBatchSize { get; set; } = 128;
    public int MaxConcurrentPredictions { get; set; } = 1;
    public bool FlashAttention { get; set; }
    public bool LockModelInMemory { get; set; }
    public bool ContextShift { get; set; } = true;
    public double Temperature { get; set; } = 0.8;
    public int MaxResponseTokens { get; set; } = 1024;
    public int ReasoningBudget { get; set; }
    public string StopStrings { get; set; } = "";
    public string SpeechVoice { get; set; } = "";
    public string SpeakerDeviceId { get; set; } = "";
    public int SpeechRate { get; set; } = 2;
    public int SpeechVolume { get; set; } = 100;
    public bool SpeakResponses { get; set; }
    public bool AutoCreateWorkspaceFiles { get; set; }
    public string MicrophoneDeviceId { get; set; } = "";
    public int MicrophoneVolume { get; set; } = 100;
    public string RecognitionCulture { get; set; } = "de-DE";
    public double RecognitionConfidence { get; set; } = 0.35;
    public string OutputFormat { get; set; } = "text";

    public static UserSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var settings = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(SettingsPath)) ?? new UserSettings();
                settings.CloudModels ??= [];
                settings.AdditionalModelDirectories ??= [];
                return settings;
            }
        }
        catch (JsonException)
        {
            // A damaged preference file should not prevent the app from opening.
        }
        catch (IOException)
        {
            // Preferences can be recreated if the file is temporarily unavailable.
        }

        return new UserSettings
        {
            ModelFile = Path.GetFileName(Environment.GetEnvironmentVariable("LOCAL_MODEL_PATH") ?? ""),
            LlamaServerPath = Environment.GetEnvironmentVariable("LLAMA_SERVER_PATH") ?? "llama-server",
            ContextLength = ReadInteger("LOCAL_CONTEXT_SIZE", 4096),
            GpuLayers = ReadInteger("LOCAL_GPU_LAYERS", 99),
            CpuThreads = ReadInteger("LOCAL_CPU_THREADS", Math.Max(1, Environment.ProcessorCount - 1)),
            BatchSize = ReadInteger("LOCAL_BATCH_SIZE", 512),
            PhysicalBatchSize = ReadInteger("LOCAL_UBATCH_SIZE", 128),
            MaxConcurrentPredictions = ReadInteger("LOCAL_PARALLEL", 1),
            FlashAttention = string.Equals(Environment.GetEnvironmentVariable("LOCAL_FLASH_ATTENTION"), "true", StringComparison.OrdinalIgnoreCase),
            LockModelInMemory = string.Equals(Environment.GetEnvironmentVariable("LOCAL_MLOCK"), "true", StringComparison.OrdinalIgnoreCase)
        };
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var temporaryPath = SettingsPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporaryPath, SettingsPath, overwrite: true);
    }

    private static int ReadInteger(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
        && value > 0 ? value : fallback;
}
