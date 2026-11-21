namespace Nova.Desktop;

internal sealed record LocalModelPreset(
    string Name,
    string ModelFile,
    string Description,
    bool SupportsVision = false,
    string? VisionProjectorFile = null,
    string? SourceDirectory = null)
{
    public string DisplayName =>
        $"{Name} · {Path.GetFileName(ModelFile)}" +
        (string.IsNullOrWhiteSpace(SourceDirectory) ? "" : $" — {SourceDirectory}");
}

internal static class LocalModelPresets
{
    public const string PocketPerformanceModelFile = "Qwen3-0.6B-Q4_K_M.gguf";
    public const string PocketUltraPerformanceModelFile = "SmolVLM2-256M-Video-Instruct-Q4_K_M.gguf";
    public const string PocketUltraPerformanceProjectorFile = "mmproj-SmolVLM2-256M-Video-Instruct-Q8_0.gguf";
    public const string PocketVisionFallbackModelFile = "SmolVLM2-500M-Video-Instruct-Q4_K_M.gguf";
    public const string PocketVisionFallbackProjectorFile = "SmolVLM2-500M-Video-Instruct-mmproj-f16.gguf";
    public const string CoderModelFile = "Qwen2.5-Coder-7B-Instruct-Q4_K_M.gguf";
    public const string VisionFallbackModelFile = PocketVisionFallbackModelFile;
    public const string VisionFallbackProjectorFile = PocketVisionFallbackProjectorFile;
    public const string LegacyVisionFallbackModelFile = "SmolVLM2-500M-Video-Instruct-Q8_0.gguf";
    public const string LegacyVisionFallbackProjectorFile = "mmproj-SmolVLM2-500M-Video-Instruct-Q8_0.gguf";

    public static IReadOnlyList<LocalModelPreset> All { get; } =
    [
        new("Pocket · Performance", PocketPerformanceModelFile,
            "0,6B · schnelles Textmodell aus Pocket Research. Bildaufgaben werden an SmolVLM2-500M weitergeleitet."),
        new("Pocket · Ultra-Performance", PocketUltraPerformanceModelFile,
            "0,25B · sehr leichtes Text- und Bildmodell aus Pocket Research; deutlich einfachere Antworten.",
            true, PocketUltraPerformanceProjectorFile),
        new("Performance", "DeepSeek-R1-Distill-Qwen-1.5B-Q8_0.gguf",
            "1,5B · schnell und speichersparend für kurze Chat-, Research- und einfache Codeaufgaben; kein spezialisiertes Coder-Modell."),
        new("Quality", "Qwen3.8-2B-Q8_0.gguf",
            "2B · ausgewogenes Textmodell für Chat, Research und einfache Codeentwürfe. Für Bilder nutzt NOVA das separate Vision-Fallback."),
        new("Ultra-Quality", "WikiQwen-4B.Q4_K_M.gguf",
            "4B · größeres, spezialisiertes WikiQwen-Modell; nicht als allgemeines Modell gedacht."),
        new("Coder · Quality", CoderModelFile,
            "7B · spezialisiertes Qwen-Coder-Modell für Codeentwürfe; benötigt deutlich mehr Arbeitsspeicher."),
        new("Vision-Fallback", VisionFallbackModelFile,
            "0,5B · leichtes, separates Bildverständnis-Modell aus Pocket Performance. NOVA nutzt es automatisch, wenn das Research-Modell keine Bilder unterstützt.",
            true, VisionFallbackProjectorFile)
    ];
}
