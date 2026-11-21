namespace Nova.Desktop;

internal sealed record CloudProviderDefinition(
    string Id,
    string Name,
    Uri Endpoint,
    string DefaultModel,
    string ApiKeyEnvironmentVariable,
    string ModelHelp);

internal static class CloudProviderCatalog
{
    private static readonly CloudProviderDefinition[] Providers =
    [
        new(
            "gemini",
            "Google Gemini",
            new Uri("https://generativelanguage.googleapis.com/v1beta/openai/chat/completions"),
            "gemini-3.8-flash",
            "GEMINI_API_KEY",
            "Schnelle multimodale Zusammenfassungen. API-Schlüssel in Google AI Studio erstellen."),
        new(
            "deepseek",
            "DeepSeek",
            new Uri("https://api.deepseek.com/chat/completions"),
            "deepseek-flash",
            "DEEPSEEK_API_KEY",
            "Preisbewusste Textzusammenfassungen. API-Schlüssel im DeepSeek-Platform-Konto erstellen."),
        new(
            "openrouter",
            "OpenRouter",
            new Uri("https://openrouter.ai/api/v1/chat/completions"),
            "",
            "OPENROUTER_API_KEY",
            "Viele Anbieter und Modelle über eine Schnittstelle. Ohne Modell-ID wird das Konto-Standardmodell genutzt."),
        new(
            "huggingface",
            "Hugging Face",
            new Uri("https://router.huggingface.co/v1/chat/completions"),
            "Qwen/Qwen2.5-7B-Instruct",
            "HF_TOKEN",
            "Modellauswahl und Abrechnung erfolgen über Hugging Face Inference Providers.")
    ];

    public static IReadOnlyList<CloudProviderDefinition> All => Providers;

    public static bool IsCloudProvider(string? providerId) =>
        Providers.Any(provider => string.Equals(provider.Id, providerId, StringComparison.Ordinal));

    public static CloudProviderDefinition Get(string providerId) =>
        Providers.FirstOrDefault(provider => string.Equals(provider.Id, providerId, StringComparison.Ordinal))
        ?? throw new InvalidOperationException($"Unbekannter Cloudanbieter: {providerId}");

    public static string GetModel(UserSettings settings, string providerId)
    {
        var provider = Get(providerId);
        if (settings.CloudModels.TryGetValue(providerId, out var configuredModel)
            && !string.IsNullOrWhiteSpace(configuredModel))
            return configuredModel.Trim();

        if (providerId == "huggingface")
            return Environment.GetEnvironmentVariable("HF_MODEL") ?? provider.DefaultModel;
        return provider.DefaultModel;
    }

    public static string? GetApiKey(string providerId)
    {
        var provider = Get(providerId);
        return CloudCredentialStore.Read(providerId)
            ?? Environment.GetEnvironmentVariable(provider.ApiKeyEnvironmentVariable);
    }

    public static void SaveModel(UserSettings settings, string providerId, string model)
    {
        settings.CloudModels[providerId] = model.Trim();
    }
}
