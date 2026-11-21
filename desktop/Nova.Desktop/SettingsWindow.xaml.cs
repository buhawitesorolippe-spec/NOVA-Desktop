using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using System.Globalization;
using System.ComponentModel;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using Microsoft.Win32;

namespace Nova.Desktop;

public partial class SettingsWindow : Window
{
    private readonly UserSettings _settings;
    private readonly SpeechOutputService _previewOutput = new();
    private SpeechRecognitionEngine? _microphoneTest;
    private MicrophoneCaptureSession? _microphoneSession;
    private bool _microphoneTestRecognized;
    private bool _loadingCloudFields;
    private string? _loadedCloudProvider;
    private readonly Dictionary<string, string> _cloudApiKeyDrafts = new(StringComparer.Ordinal);
    private readonly HashSet<string> _cloudApiKeysToDelete = new(StringComparer.Ordinal);

    public SettingsWindow(UserSettings settings)
    {
        InitializeComponent();
        ThemeManager.TrackWindow(this);
        _settings = settings;
        ThemeCombo.SelectedValuePath = "Tag";
        ThemeCombo.SelectedValue = string.Equals(settings.Theme, "Light", StringComparison.OrdinalIgnoreCase)
            ? "Light"
            : "Dark";
        ProviderCombo.SelectedValuePath = "Tag";
        ProviderCombo.SelectedValue = settings.Provider;
        LlamaPathInput.Text = settings.LlamaServerPath;
        VisionProjectorPathInput.Text = settings.VisionProjectorPath;
        ModelPresetCombo.ItemsSource = LocalModelPresets.All;
        ModelCombo.SelectedValuePath = nameof(ModelFileInfo.FullPath);
        _settings.AdditionalModelDirectories = ModelDirectory
            .NormalizeAdditionalDirectories(settings.AdditionalModelDirectories).ToList();
        AdditionalModelDirectoriesList.ItemsSource = _settings.AdditionalModelDirectories;
        RefreshRuntimes(settings.LlamaServerPath);
        UpdateRuntimeDetails(settings.LlamaServerPath);
        LoadModels();
        ModelCombo.SelectedValue = ModelDirectory.GetModelPath(
            settings.ModelFile, _settings.AdditionalModelDirectories);
        SystemPromptInput.Text = settings.SystemPrompt;
        ContextLengthInput.Text = settings.ContextLength.ToString();
        GpuLayersInput.Text = settings.GpuLayers.ToString();
        CpuThreadsInput.Text = settings.CpuThreads.ToString();
        BatchSizeInput.Text = settings.BatchSize.ToString();
        PhysicalBatchInput.Text = settings.PhysicalBatchSize.ToString();
        ParallelInput.Text = settings.MaxConcurrentPredictions.ToString();
        FlashAttentionCheck.IsChecked = settings.FlashAttention;
        MemoryLockCheck.IsChecked = settings.LockModelInMemory;
        ContextShiftCheck.IsChecked = settings.ContextShift;
        TemperatureInput.Text = settings.Temperature.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        MaxTokensInput.Text = settings.MaxResponseTokens.ToString();
        ReasoningBudgetInput.Text = settings.ReasoningBudget.ToString();
        StopStringsInput.Text = settings.StopStrings;
        AutoCreateWorkspaceFilesCheck.IsChecked = settings.AutoCreateWorkspaceFiles;
        SpeakResponsesCheck.IsChecked = settings.SpeakResponses;
        MicrophoneDeviceCombo.ItemsSource = AudioDeviceCatalog.GetCaptureDevices();
        MicrophoneDeviceCombo.SelectedValuePath = nameof(AudioDevice.Id);
        MicrophoneDeviceCombo.SelectedValue = settings.MicrophoneDeviceId;
        if (MicrophoneDeviceCombo.SelectedIndex < 0 && MicrophoneDeviceCombo.Items.Count > 0)
            MicrophoneDeviceCombo.SelectedIndex = 0;
        SpeakerDeviceCombo.ItemsSource = AudioDeviceCatalog.GetPlaybackDevices();
        SpeakerDeviceCombo.SelectedValuePath = nameof(AudioDevice.Id);
        SpeakerDeviceCombo.SelectedValue = settings.SpeakerDeviceId;
        if (SpeakerDeviceCombo.SelectedIndex < 0 && SpeakerDeviceCombo.Items.Count > 0)
            SpeakerDeviceCombo.SelectedIndex = 0;
        MicrophoneVolumeSlider.Value = Math.Clamp(settings.MicrophoneVolume, 0, 200);
        using var speechSynthesizer = new SpeechSynthesizer();
        SpeechVoiceCombo.ItemsSource = new[] { "Automatisch (Systemstimme)" }
            .Concat(speechSynthesizer.GetInstalledVoices()
                .Where(voice => voice.Enabled)
                .Select(voice => voice.VoiceInfo.Name))
            .ToArray();
        SpeechVoiceCombo.SelectedItem = string.IsNullOrWhiteSpace(settings.SpeechVoice)
            ? "Automatisch (Systemstimme)"
            : settings.SpeechVoice;
        SpeechRateSlider.Value = Math.Clamp(settings.SpeechRate, -10, 10);
        SpeechVolumeSlider.Value = Math.Clamp(settings.SpeechVolume, 0, 100);
        RecognitionCultureCombo.SelectedValuePath = "Tag";
        RecognitionCultureCombo.SelectedValue = settings.RecognitionCulture;
        RecognitionConfidenceInput.Text = settings.RecognitionConfidence.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        OutputFormatCombo.SelectedValuePath = "Tag";
        OutputFormatCombo.SelectedValue = settings.OutputFormat;
        UpdateProviderControls();
        LoadCloudProviderInputs();
        _previewOutput.Error += message => Dispatcher.InvokeAsync(() => ShowError(message));
        Closed += (_, _) =>
        {
            _microphoneTest?.RecognizeAsyncCancel();
            _microphoneTest?.Dispose();
            _microphoneSession?.Dispose();
            _previewOutput.Dispose();
        };
    }

    public UserSettings? SavedSettings { get; private set; }

    private void LoadModels()
    {
        var selected = ModelCombo.SelectedValue as string ?? _settings.ModelFile;
        var discovery = ModelDirectory.Discover(_settings.AdditionalModelDirectories);
        ModelCombo.ItemsSource = discovery.Models;
        var selectedPath = ModelDirectory.GetModelPath(selected, _settings.AdditionalModelDirectories);
        if (selectedPath is not null)
            ModelCombo.SelectedValue = selectedPath;

        ModelDirectoryStatusText.Text = discovery.Errors.Count > 0
            ? "Nicht erreichbare Modellordner: " + string.Join(" · ", discovery.Errors)
            : selectedPath is null && !string.IsNullOrWhiteSpace(selected)
                ? $"Das gespeicherte Modell ist nicht erreichbar: {selected}"
                : $"{discovery.Models.Count} GGUF-Modell(e) gefunden. Unterordner werden nicht durchsucht.";
    }

    private void AddModelDirectory_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Zusätzlichen GGUF-Modellordner auswählen",
            InitialDirectory = ModelDirectory.PathName
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var directory = Path.GetFullPath(dialog.FolderName);
            if (!Directory.Exists(directory))
            {
                ShowError($"Der Modellordner ist nicht erreichbar: {directory}");
                return;
            }

            if (_settings.AdditionalModelDirectories.Contains(directory, StringComparer.OrdinalIgnoreCase))
            {
                ModelDirectoryStatusText.Text = "Dieser Modellordner ist bereits hinzugefügt.";
                return;
            }

            _settings.AdditionalModelDirectories = ModelDirectory
                .NormalizeAdditionalDirectories(_settings.AdditionalModelDirectories.Append(directory))
                .ToList();
            AdditionalModelDirectoriesList.ItemsSource = _settings.AdditionalModelDirectories;
            LoadModels();
        }
        catch (Exception error) when (
            error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            ShowError($"Modellordner konnte nicht hinzugefügt werden: {error.Message}");
        }
    }

    private void RemoveModelDirectory_Click(object sender, RoutedEventArgs e)
    {
        if (AdditionalModelDirectoriesList.SelectedItem is not string directory)
            return;

        _settings.AdditionalModelDirectories = _settings.AdditionalModelDirectories
            .Where(item => !string.Equals(item, directory, StringComparison.OrdinalIgnoreCase))
            .ToList();
        AdditionalModelDirectoriesList.ItemsSource = _settings.AdditionalModelDirectories;
        LoadModels();
    }

    private void UpdateProviderControls()
    {
        var isLocal = Equals(ProviderCombo.SelectedValue, "local");
        LocalOptions.Visibility = isLocal ? Visibility.Visible : Visibility.Collapsed;
        CloudOptions.Visibility = isLocal ? Visibility.Collapsed : Visibility.Visible;
        PerformanceCard.Visibility = isLocal ? Visibility.Visible : Visibility.Collapsed;
        ReasoningBudgetRow.Visibility = isLocal ? Visibility.Visible : Visibility.Collapsed;
        ReasoningHelp.Visibility = isLocal ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ProviderCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsInitialized)
        {
            RememberCloudProviderDraft();
            UpdateProviderControls();
            LoadCloudProviderInputs();
        }
    }

    private void RememberCloudProviderDraft()
    {
        if (_loadedCloudProvider is not { } providerId)
            return;
        CloudProviderCatalog.SaveModel(_settings, providerId, CloudModelInput.Text);
        if (!string.IsNullOrWhiteSpace(CloudApiKeyInput.Password))
            _cloudApiKeyDrafts[providerId] = CloudApiKeyInput.Password;
    }

    private void LoadCloudProviderInputs()
    {
        var providerId = ProviderCombo.SelectedValue as string;
        _loadedCloudProvider = CloudProviderCatalog.IsCloudProvider(providerId) ? providerId : null;
        if (_loadedCloudProvider is not { } selectedProvider)
            return;

        _loadingCloudFields = true;
        try
        {
            CloudModelInput.Text = CloudProviderCatalog.GetModel(_settings, selectedProvider);
            if (_cloudApiKeyDrafts.TryGetValue(selectedProvider, out var draft))
            {
                CloudApiKeyInput.Password = draft;
            }
            else
            {
                var storedKey = CloudCredentialStore.Read(selectedProvider);
                CloudApiKeyInput.Password = storedKey ?? "";
                if (storedKey is not null)
                    _cloudApiKeyDrafts[selectedProvider] = storedKey;
            }

            var definition = CloudProviderCatalog.Get(selectedProvider);
            CloudKeyStatus.Text = _cloudApiKeysToDelete.Contains(selectedProvider)
                ? "Der gespeicherte Schlüssel wird beim Speichern gelöscht."
                : !string.IsNullOrWhiteSpace(CloudApiKeyInput.Password)
                    ? "Schlüssel im Windows-Anmeldeinformationsspeicher gefunden."
                    : Environment.GetEnvironmentVariable(definition.ApiKeyEnvironmentVariable) is { Length: > 0 }
                        ? $"Kein gespeicherter Schlüssel. {definition.ApiKeyEnvironmentVariable} aus .env wird verwendet."
                        : "Noch kein Schlüssel gespeichert.";
            CloudHelp.Text = definition.ModelHelp;
        }
        catch (Win32Exception error)
        {
            CloudApiKeyInput.Clear();
            CloudKeyStatus.Text = "Der Windows-Anmeldeinformationsspeicher ist nicht verfügbar.";
            ShowError(error.Message);
        }
        finally
        {
            _loadingCloudFields = false;
        }
    }

    private void CloudApiKeyInput_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_loadingCloudFields || _loadedCloudProvider is not { } providerId
            || string.IsNullOrWhiteSpace(CloudApiKeyInput.Password))
            return;

        _cloudApiKeyDrafts[providerId] = CloudApiKeyInput.Password;
        _cloudApiKeysToDelete.Remove(providerId);
        CloudKeyStatus.Text = "Der Schlüssel wird beim Speichern im Windows-Anmeldeinformationsspeicher abgelegt.";
    }

    private void ClearCloudApiKey_Click(object sender, RoutedEventArgs e)
    {
        if (_loadedCloudProvider is not { } providerId)
            return;

        _cloudApiKeyDrafts.Remove(providerId);
        _cloudApiKeysToDelete.Add(providerId);
        CloudApiKeyInput.Clear();
        var environmentVariable = CloudProviderCatalog.Get(providerId).ApiKeyEnvironmentVariable;
        CloudKeyStatus.Text = Environment.GetEnvironmentVariable(environmentVariable) is { Length: > 0 }
            ? $"Der gespeicherte Schlüssel wird gelöscht; {environmentVariable} aus .env bleibt als Fallback aktiv."
            : "Der gespeicherte Schlüssel wird beim Speichern gelöscht.";
    }

    private void RefreshModels_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            LoadModels();
            SettingsError.Visibility = Visibility.Collapsed;
        }
        catch (IOException error)
        {
            ShowError($"Der models-Ordner konnte nicht gelesen werden: {error.Message}");
        }
        catch (UnauthorizedAccessException error)
        {
            ShowError($"Kein Zugriff auf den models-Ordner: {error.Message}");
        }
    }

    private void BrowseLlama_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "llama-server auswählen",
            Filter = "llama-server (llama-server.exe)|llama-server.exe|Programme (*.exe)|*.exe|Alle Dateien (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true)
        {
            LlamaPathInput.Text = dialog.FileName;
            RuntimeCombo.SelectedItem = null;
            UpdateRuntimeDetails(dialog.FileName);
        }
    }

    private void BrowseVisionProjector_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Vision-Projektordatei auswählen",
            Filter = "Vision-Projektor (*.gguf)|*.gguf|Alle Dateien (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true)
            VisionProjectorPathInput.Text = dialog.FileName;
    }

    private void RuntimeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RuntimeCombo.SelectedItem is LlamaRuntime runtime)
        {
            LlamaPathInput.Text = runtime.ExecutablePath;
            UpdateRuntimeDetails(runtime.ExecutablePath);
        }
    }

    private void LlamaPathInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsInitialized)
            return;
        if (RuntimeCombo.SelectedItem is LlamaRuntime runtime
            && !string.Equals(LlamaPathInput.Text.Trim().Trim('"'), runtime.ExecutablePath,
                StringComparison.OrdinalIgnoreCase))
            RuntimeCombo.SelectedItem = null;
        UpdateRuntimeDetails(LlamaPathInput.Text.Trim().Trim('"'));
    }

    private void ApplyModelPreset_Click(object sender, RoutedEventArgs e)
    {
        if (!Equals(ProviderCombo.SelectedValue, "local"))
        {
            ShowError("Die lokalen Modell-Presets können nur mit dem Anbieter „Lokal“ verwendet werden.");
            return;
        }
        if (ModelPresetCombo.SelectedItem is not LocalModelPreset preset)
        {
            ShowError("Wähle zuerst ein Modell-Preset aus.");
            return;
        }
        var modelPath = ModelDirectory.GetModelPath(
            preset.ModelFile, _settings.AdditionalModelDirectories);
        if (modelPath is null)
        {
            ShowError($"Das Preset-Modell „{preset.ModelFile}“ ist in den ausgewählten Modellordnern nicht verfügbar. NOVA lädt keine Modelle automatisch herunter.");
            return;
        }

        ModelCombo.SelectedValue = modelPath;
        ModelPresetHelp.Text = $"{preset.Name}: {preset.ModelFile} — {preset.Description} " +
                               "Laufzeit, Anbieter und manuelle Hardwarewerte wurden nicht geändert.";
        SettingsError.Visibility = Visibility.Collapsed;
    }

    private void ModelPresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModelPresetCombo.SelectedItem is LocalModelPreset preset)
            ModelPresetHelp.Text =
                $"{preset.ModelFile} — {preset.Description} Laufzeit und manuelle Hardwarewerte bleiben unverändert.";
        else
            ModelPresetHelp.Text =
                "Presets wählen ausschließlich ein bereits verfügbares GGUF-Modell; Laufzeit und manuelle Hardwarewerte bleiben unverändert.";
    }

    private void UpdateRuntimeDetails(string configuredPath)
    {
        var runtime = (RuntimeCombo.ItemsSource as IReadOnlyList<LlamaRuntime>)?
            .FirstOrDefault(candidate => string.Equals(
                candidate.ExecutablePath, configuredPath, StringComparison.OrdinalIgnoreCase));
        RuntimeDetailsText.Text = runtime is null
            ? $"Benutzerdefinierte Runtime (Backend nicht verifiziert): {configuredPath}"
            : $"Ausgewählt: {runtime.Kind} · {runtime.Name}\n{runtime.ExecutablePath}";
    }

    private void ImportRuntimes_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var imported = LlamaRuntimeStore.ImportArchives();
            RefreshRuntimes(LlamaPathInput.Text);
            SettingsError.Text = imported.Count == 0
                ? $"Keine neuen ZIPs gefunden. Lege Archive in {LlamaRuntimeStore.RootPath}\\CPU, CUDA oder Vulkan ab."
                : $"{imported.Count} Runtime-ZIP(s) entpackt. Wähle unten die gewünschte Version aus.";
            SettingsError.SetResourceReference(
                TextBlock.ForegroundProperty, imported.Count == 0 ? "MutedBrush" : "AccentBrush");
            SettingsError.Visibility = Visibility.Visible;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ShowError($"llama.cpp-ZIP konnte nicht eingelesen werden: {error.Message}");
        }
    }

    private void RefreshRuntimes(string selectedPath)
    {
        RuntimeCombo.ItemsSource = LlamaRuntimeStore.GetRuntimes();
        RuntimeCombo.SelectedItem = (RuntimeCombo.ItemsSource as IReadOnlyList<LlamaRuntime>)?
            .FirstOrDefault(runtime => string.Equals(runtime.ExecutablePath, selectedPath, StringComparison.OrdinalIgnoreCase));
    }

    private void TestVoice_Click(object sender, RoutedEventArgs e)
    {
        _previewOutput.CancelAll();
        _previewOutput.Configure(GetAudioSettings());
        _previewOutput.Speak("Dies ist eine Stimmprobe von NOVA.");
    }

    private void TestMicrophone_Click(object sender, RoutedEventArgs e)
    {
        if (_microphoneTest is not null)
            return;
        try
        {
            if (MicrophoneDeviceCombo.SelectedItem is not AudioDevice selectedDevice)
            {
                ShowError("Es wurde kein aktives Mikrofon gefunden.");
                return;
            }
            var culture = CultureInfo.GetCultureInfo(RecognitionCultureCombo.SelectedValue as string ?? "de-DE");
            var recognizer = new SpeechRecognitionEngine(culture);
            _microphoneTest = recognizer;
            _microphoneTestRecognized = false;
            _microphoneSession = new MicrophoneCaptureSession(
                selectedDevice.Id, (int)Math.Round(MicrophoneVolumeSlider.Value));
            recognizer.LoadGrammar(new DictationGrammar());
            recognizer.SetInputToAudioStream(_microphoneSession.AudioStream, MicrophoneCaptureSession.SpeechFormat);
            recognizer.SpeechRecognized += (_, args) => Dispatcher.Invoke(() =>
            {
                if (!string.IsNullOrWhiteSpace(args.Result.Text))
                {
                    _microphoneTestRecognized = true;
                    MessageBox.Show(this, $"Erkannt:\n\n{args.Result.Text}", "Mikrofontest",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            });
            recognizer.RecognizeCompleted += (_, _) => Dispatcher.Invoke(() =>
            {
                if (ReferenceEquals(_microphoneTest, recognizer))
                {
                    _microphoneTest = null;
                    recognizer.Dispose();
                    _microphoneSession?.Dispose();
                    _microphoneSession = null;
                    if (!_microphoneTestRecognized)
                        MessageBox.Show(this,
                            "Es wurde keine Sprache erkannt. Prüfe Mikrofon, Eingangspegel und Erkennungssprache.",
                            "Mikrofontest", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            });
            MessageBox.Show(this, "Sprich jetzt einen kurzen Satz. Die Erkennung verwendet das Windows-Standardmikrofon.",
                "Mikrofontest", MessageBoxButton.OK, MessageBoxImage.Information);
            _microphoneSession.Start();
            recognizer.RecognizeAsync(RecognizeMode.Single);
        }
        catch (Exception error) when (error is InvalidOperationException
                                      or ArgumentException
                                      or NotSupportedException
                                      or System.Runtime.InteropServices.COMException)
        {
            _microphoneTest?.Dispose();
            _microphoneTest = null;
            _microphoneSession?.Dispose();
            _microphoneSession = null;
            ShowError($"Mikrofontest konnte nicht gestartet werden: {error.Message}");
        }
    }

    private UserSettings GetAudioSettings() => new()
    {
        SpeechVoice = SpeechVoiceCombo.SelectedIndex <= 0
            ? ""
            : SpeechVoiceCombo.SelectedItem as string ?? "",
        SpeechRate = (int)Math.Round(SpeechRateSlider.Value),
        SpeechVolume = (int)Math.Round(SpeechVolumeSlider.Value),
        SpeakerDeviceId = (SpeakerDeviceCombo.SelectedItem as AudioDevice)?.Id ?? ""
    };

    private void OpenWindowsAudioSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:sound") { UseShellExecute = true });
        }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            ShowError($"Windows-Audioeinstellungen konnten nicht geöffnet werden: {error.Message}");
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var provider = ProviderCombo.SelectedValue as string;
        var isLocal = provider == "local";
        if (!isLocal && !CloudProviderCatalog.IsCloudProvider(provider))
        {
            ShowError("Bitte wähle einen Modell-Anbieter aus.");
            return;
        }

        var modelFile = isLocal ? ModelCombo.SelectedValue as string ?? "" : "";
        if (isLocal && modelFile.Length == 0)
        {
            ShowError("Wähle zuerst ein erreichbares GGUF-Modell aus den Modellordnern aus.");
            return;
        }
        if (isLocal && ModelDirectory.GetModelPath(modelFile, _settings.AdditionalModelDirectories) is null)
        {
            ShowError($"Das ausgewählte GGUF-Modell ist nicht mehr erreichbar: {modelFile}");
            return;
        }
        var cloudModel = isLocal ? "" : CloudModelInput.Text.Trim();
        if (!isLocal && cloudModel.Length == 0 && provider != "openrouter")
        {
            ShowError("Gib die Modell-ID des ausgewählten Cloudanbieters ein.");
            return;
        }
        if (!isLocal)
        {
            try
            {
                var definition = CloudProviderCatalog.Get(provider!);
                var environmentKey = Environment.GetEnvironmentVariable(definition.ApiKeyEnvironmentVariable);
                var apiKey = CloudApiKeyInput.Password.Trim();
                if (apiKey.Length == 0 && !_cloudApiKeysToDelete.Contains(provider!))
                    apiKey = CloudCredentialStore.Read(provider!) ?? environmentKey ?? "";
                else if (_cloudApiKeysToDelete.Contains(provider!))
                    apiKey = environmentKey ?? "";

                if (apiKey.Length == 0)
                {
                    ShowError($"Für {definition.Name} fehlt ein API-Schlüssel. Trage ihn oben ein oder setze {definition.ApiKeyEnvironmentVariable} in .env.");
                    return;
                }
            }
            catch (Win32Exception error)
            {
                ShowError($"Der Cloud-API-Schlüssel konnte nicht geprüft werden: {error.Message}");
                return;
            }
        }

        if (!TryReadInteger(ContextLengthInput, "Kontextlänge", 512, 131072, out var contextLength)
            || !TryReadInteger(GpuLayersInput, "GPU-Layer", 0, 999, out var gpuLayers)
            || !TryReadInteger(CpuThreadsInput, "CPU-Threads", 1, 1024, out var cpuThreads)
            || !TryReadInteger(BatchSizeInput, "Batch-Größe", 1, 65536, out var batchSize)
            || !TryReadInteger(PhysicalBatchInput, "Physische Batch-Größe", 1, 65536, out var physicalBatch)
            || !TryReadInteger(ParallelInput, "Parallele Anfragen", 1, 128, out var parallel)
            || !TryReadInteger(MaxTokensInput, "Maximale Antwortlänge", 1, 131072, out var maxTokens)
            || !TryReadInteger(ReasoningBudgetInput, "Denkbudget", 0, 131072, out var reasoningBudget))
            return;

        if (physicalBatch > batchSize)
        {
            ShowError("Die physische Batch-Größe darf nicht größer als die Batch-Größe sein.");
            return;
        }
        var temperatureText = TemperatureInput.Text.Trim().Replace(',', '.');
        if (!double.TryParse(temperatureText, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var temperature)
            || temperature is < 0 or > 2)
        {
            ShowError("Die Temperatur muss eine Zahl zwischen 0 und 2 sein (z. B. 0,8 oder 0.8).");
            return;
        }
        var confidenceText = RecognitionConfidenceInput.Text.Trim().Replace(',', '.');
        if (!double.TryParse(confidenceText, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var confidence)
            || confidence is < 0 or > 1)
        {
            ShowError("Die Erkennungskonfidenz muss eine Zahl zwischen 0 und 1 sein.");
            return;
        }
        if (OutputFormatCombo.SelectedValue is not ("text" or "json"))
        {
            ShowError("Bitte wähle ein Antwortformat aus.");
            return;
        }

        var llamaPath = string.IsNullOrWhiteSpace(LlamaPathInput.Text) ? "llama-server" : LlamaPathInput.Text.Trim().Trim('"');
        if (isLocal && !MainWindow.IsLlamaServerAvailable(llamaPath))
        {
            ShowError("llama-server.exe wurde nicht gefunden. Ein GGUF-Modell allein reicht zum Ausführen nicht: Lade llama.cpp für Windows herunter und wähle darin llama-server.exe aus. Alternativ muss der Befehl llama-server über PATH erreichbar sein.");
            return;
        }
        var visionProjectorPath = VisionProjectorPathInput.Text.Trim().Trim('"');
        if (isLocal && visionProjectorPath.Length > 0 && !File.Exists(visionProjectorPath))
        {
            ShowError("Die ausgewählte Vision-Projektordatei wurde nicht gefunden.");
            return;
        }

        _settings.Provider = provider!;
        _settings.AdditionalModelDirectories = ModelDirectory
            .NormalizeAdditionalDirectories(_settings.AdditionalModelDirectories).ToList();
        _settings.Theme = ThemeCombo.SelectedValue as string ?? "Dark";
        _settings.ModelFile = modelFile;
        if (!isLocal)
            CloudProviderCatalog.SaveModel(_settings, provider!, cloudModel);
        _settings.LlamaServerPath = llamaPath;
        _settings.VisionProjectorPath = visionProjectorPath;
        _settings.SystemPrompt = SystemPromptInput.Text.Trim();
        _settings.ContextLength = contextLength;
        _settings.GpuLayers = gpuLayers;
        _settings.CpuThreads = cpuThreads;
        _settings.BatchSize = batchSize;
        _settings.PhysicalBatchSize = physicalBatch;
        _settings.MaxConcurrentPredictions = parallel;
        _settings.FlashAttention = FlashAttentionCheck.IsChecked == true;
        _settings.LockModelInMemory = MemoryLockCheck.IsChecked == true;
        _settings.ContextShift = ContextShiftCheck.IsChecked == true;
        _settings.Temperature = temperature;
        _settings.MaxResponseTokens = maxTokens;
        _settings.ReasoningBudget = reasoningBudget;
        _settings.StopStrings = StopStringsInput.Text;
        _settings.AutoCreateWorkspaceFiles = AutoCreateWorkspaceFilesCheck.IsChecked == true;
        _settings.SpeakResponses = SpeakResponsesCheck.IsChecked == true;
        _settings.SpeechVoice = SpeechVoiceCombo.SelectedIndex <= 0
            ? ""
            : SpeechVoiceCombo.SelectedItem as string ?? "";
        _settings.SpeechRate = (int)Math.Round(SpeechRateSlider.Value);
        _settings.SpeechVolume = (int)Math.Round(SpeechVolumeSlider.Value);
        _settings.RecognitionCulture = RecognitionCultureCombo.SelectedValue as string ?? "de-DE";
        _settings.RecognitionConfidence = confidence;
        _settings.OutputFormat = OutputFormatCombo.SelectedValue as string ?? "text";
        _settings.MicrophoneDeviceId = (MicrophoneDeviceCombo.SelectedItem as AudioDevice)?.Id ?? "";
        _settings.MicrophoneVolume = (int)Math.Round(MicrophoneVolumeSlider.Value);
        _settings.SpeakerDeviceId = (SpeakerDeviceCombo.SelectedItem as AudioDevice)?.Id ?? "";
        try
        {
            foreach (var providerId in _cloudApiKeysToDelete)
                CloudCredentialStore.Delete(providerId);
            foreach (var (providerId, apiKey) in _cloudApiKeyDrafts)
            {
                if (!_cloudApiKeysToDelete.Contains(providerId) && !string.IsNullOrWhiteSpace(apiKey))
                    CloudCredentialStore.Save(providerId, apiKey);
            }
            _settings.Save();
            SavedSettings = _settings;
            DialogResult = true;
        }
        catch (IOException error)
        {
            ShowError($"Einstellungen konnten nicht gespeichert werden: {error.Message}");
        }
        catch (UnauthorizedAccessException error)
        {
            ShowError($"Einstellungen konnten nicht gespeichert werden: {error.Message}");
        }
        catch (Win32Exception error)
        {
            ShowError($"Cloud-Schlüssel konnten nicht sicher gespeichert werden: {error.Message}");
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void ShowError(string message)
    {
        SettingsError.Text = message;
        SettingsError.Visibility = Visibility.Visible;
    }

    private bool TryReadInteger(TextBox input, string label, int minimum, int maximum, out int value)
    {
        if (int.TryParse(input.Text, out value) && value >= minimum && value <= maximum)
            return true;
        ShowError($"{label} muss eine ganze Zahl zwischen {minimum} und {maximum} sein.");
        input.Focus();
        input.SelectAll();
        return false;
    }
}
