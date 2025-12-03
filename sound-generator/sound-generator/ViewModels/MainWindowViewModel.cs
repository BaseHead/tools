using System;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using AISoundGenerator.Services;
using AISoundGenerator.Models;

namespace AISoundGenerator.ViewModels;

public class AppSettings
{
    public double DurationSeconds { get; set; } = 0.5;
    public double PromptInfluence { get; set; } = 0.3;
    public int Variations { get; set; } = 4;
    public GenerationType GenerationType { get; set; } = GenerationType.SFX;
    public string? LastSelectedVoiceId { get; set; }
    public double VoiceStability { get; set; } = 0.5;
    public double VoiceSimilarity { get; set; } = 0.5;
    public double VoiceExpression { get; set; } = 0.0;
    public bool SpeakerBoost { get; set; } = false;
    public string? SelectedModel { get; set; } = "eleven_monolingual_v1";
    public string? SelectedQuality { get; set; } = "16kHz/16bit";
    public string? SelectedAudioFormat { get; set; } = "mp3";
    public string? LastSpeechAudioFormat { get; set; } = "mp3"; // Store last speech format
    public int SeedValue { get; set; } = 0; // Changed from string to int
}

public partial class MainWindowViewModel : ViewModelBase
{
    private const string ELEVENLABS_SIGNUP_URL = "https://try.elevenlabs.io/o5j5ndwlvpdv";
    private const int DEFAULT_VOICE_INDEX = 3;
    private const bool WASABI_ENABLED = true;
    
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly IDialogService _dialogService;
    private readonly IFileService _fileService;
    private readonly IBrowserService _browserService;
    private readonly IApiService _apiService;
    private readonly WasabiStorageService _wasabiStorageService;
    
    // Store the last selected audio format for speech mode
    private string lastSpeechAudioFormat = "mp3";

    [ObservableProperty]
    private string apiKey = string.Empty;

    // Computed property to determine if the API key is empty
    public bool IsApiKeyEmpty => string.IsNullOrWhiteSpace(ApiKey);

    [ObservableProperty]
    private string promptText = string.Empty;

    [ObservableProperty]
    private double durationSeconds = 0.5;

    [ObservableProperty]
    private double promptInfluence = 0.3;

    [ObservableProperty]
    private int variations = 4;

    [ObservableProperty]
    private bool isGenerating;

    [ObservableProperty]
    private double progressValue;

    [ObservableProperty]
    private string progressText = string.Empty;
    
    [ObservableProperty]
    private GenerationType generationType = GenerationType.SFX;

    [ObservableProperty]
    private ObservableCollection<Models.VoiceModel> availableVoices = new();

    [ObservableProperty]
    private Models.VoiceModel? selectedVoice;

    [ObservableProperty]
    private bool isLoadingVoices;

    [ObservableProperty]
    private double voiceStability = 0.5;

    [ObservableProperty]
    private double voiceSimilarity = 0.5;

    [ObservableProperty]
    private double voiceExpression = 0.0;

    [ObservableProperty]
    private bool speakerBoost = false;

    [ObservableProperty]
    private string selectedModel = "eleven_monolingual_v1";

    [ObservableProperty]
    private string selectedQuality = "16kHz/16bit";

    [ObservableProperty]
    private string selectedAudioFormat = "mp3";

    [ObservableProperty]
    private int seedValue = 0;

    // Display property for the seed value that shows "Random" when 0
    public string DisplaySeedValue => SeedValue == 0 ? "Random" : (SeedValue / 1000.0).ToString("F1");

    // Add new commands for the UI interaction
    [RelayCommand]
    private async Task Generate() => await GenerateSoundEffects();
    
    [RelayCommand]
    private void Cancel() => CancelGeneration();
    
    [RelayCommand]
    private void GetApiKey() => OpenApiKeyUrl();

    private List<string>? _availableModels;
    public List<string> AvailableModels => _availableModels ??= new List<string>
    {
        "eleven_monolingual_v1",     // Legacy model
        "eleven_multilingual_v2",    // Latest model with multilingual support
        "eleven_turbo_v2"           // Fast, high-quality model
    };

    private List<string>? _qualityOptions;
    public List<string> QualityOptions => _qualityOptions ??= new List<string>
    {
        "16kHz/16bit", 
        "32kHz/24bit",
        "48kHz/24bit"
    };

    private List<string>? _audioFormats;
    public List<string> AudioFormats => _audioFormats ??= new List<string>
    {
        "mp3",
        "wav",
        "ogg"
    };

    public string Greeting => "basehead AI Sound Generator";

    private List<double>? _promptInfluenceValues;
    public List<double> PromptInfluenceValues => _promptInfluenceValues ??= Enumerable.Range(0, 11)
        .Select(x => Math.Round(x * 0.1, 1))
        .ToList();

    private List<int>? _variationValues;
    public List<int> VariationValues => _variationValues ??= Enumerable.Range(1, 10).ToList();

    // Constructor with dependency injection
    public MainWindowViewModel(
        IDialogService dialogService,
        IFileService fileService,
        IBrowserService browserService,
        IApiService apiService,
        WasabiStorageService wasabiStorageService)
    {
        _dialogService = dialogService;
        _fileService = fileService;
        _browserService = browserService;
        _apiService = apiService;
        _wasabiStorageService = wasabiStorageService;
        
        // Load settings and API key
        LoadApiKey();
        LoadSettings();
        
        // Initialize voices collection
        AvailableVoices = new ObservableCollection<Models.VoiceModel>();
        
        // Start voice loading immediately
        Dispatcher.UIThread.Post(async () => await InitializeVoicesAsync());
    }

    private void LoadApiKey()
    {
        try
        {
            var apiKeyText = _fileService.ReadTextFromFile(_fileService.GetApiKeyFilePath());
            if (!string.IsNullOrWhiteSpace(apiKeyText))
            {
                ApiKey = apiKeyText;
                _apiService.SetApiKey(ApiKey);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load API key: {ex.Message}");
        }
    }

    partial void OnApiKeyChanged(string value)
    {
        try
        {
            _fileService.SaveTextToFile(_fileService.GetApiKeyFilePath(), value);
            _apiService.SetApiKey(value);
            OnPropertyChanged(nameof(IsApiKeyEmpty));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save API key: {ex.Message}");
        }
    }

    private void LoadSettings()
    {
        try
        {
            var settingsJson = _fileService.ReadTextFromFile(_fileService.GetSettingsFilePath());
            if (!string.IsNullOrWhiteSpace(settingsJson))
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(settingsJson);
                if (settings != null)
                {
                    DurationSeconds = settings.DurationSeconds;
                    PromptInfluence = settings.PromptInfluence;
                    Variations = settings.Variations;
                    GenerationType = settings.GenerationType;
                    VoiceStability = settings.VoiceStability;
                    VoiceSimilarity = settings.VoiceSimilarity;
                    VoiceExpression = settings.VoiceExpression;
                    SpeakerBoost = settings.SpeakerBoost;
                    SelectedModel = settings.SelectedModel ?? "eleven_monolingual_v1";
                    SelectedQuality = settings.SelectedQuality ?? "16kHz/16bit";
                    SelectedAudioFormat = settings.SelectedAudioFormat ?? "mp3";
                    lastSpeechAudioFormat = settings.LastSpeechAudioFormat ?? "mp3";
                    SeedValue = settings.SeedValue;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load settings: {ex.Message}");
        }
    }

    private void SaveSettings()
    {
        try
        {
            var settings = new AppSettings
            {
                DurationSeconds = DurationSeconds,
                PromptInfluence = PromptInfluence,
                Variations = Variations,
                GenerationType = GenerationType,
                VoiceStability = VoiceStability,
                VoiceSimilarity = VoiceSimilarity,
                VoiceExpression = VoiceExpression,
                SpeakerBoost = SpeakerBoost,
                SelectedModel = SelectedModel,
                LastSelectedVoiceId = SelectedVoice?.Id,
                SelectedQuality = SelectedQuality,
                SelectedAudioFormat = SelectedAudioFormat,
                LastSpeechAudioFormat = lastSpeechAudioFormat,
                SeedValue = SeedValue
            };

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            _fileService.SaveTextToFile(_fileService.GetSettingsFilePath(), json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }

    partial void OnDurationSecondsChanged(double value) => SaveSettings();
    partial void OnPromptInfluenceChanged(double value) => SaveSettings();
    partial void OnVariationsChanged(int value) => SaveSettings();
    
    partial void OnGenerationTypeChanged(GenerationType value)
    {
        if (value == GenerationType.SFX)
        {
            // Save current audio format before switching to MP3 for SFX
            if (GenerationType != GenerationType.SFX && SelectedAudioFormat != "mp3")
            {
                lastSpeechAudioFormat = SelectedAudioFormat;
            }
            
            // Always use MP3 for SFX as it's the only supported format
            SelectedAudioFormat = "mp3";
        }
        else
        {
            // Restore the last used audio format for speech
            SelectedAudioFormat = lastSpeechAudioFormat;
        }
        
        SaveSettings();
    }
    
    partial void OnSelectedVoiceChanged(Models.VoiceModel? value) => SaveSettings();
    partial void OnVoiceStabilityChanged(double value) => SaveSettings();
    partial void OnVoiceSimilarityChanged(double value) => SaveSettings();
    partial void OnVoiceExpressionChanged(double value) => SaveSettings();
    partial void OnSpeakerBoostChanged(bool value) => SaveSettings();
    partial void OnSelectedModelChanged(string value) => SaveSettings();
    partial void OnSelectedQualityChanged(string value) => SaveSettings();
    
    partial void OnSelectedAudioFormatChanged(string value)
    {
        // Remember the last audio format chosen for speech mode
        if (GenerationType == GenerationType.Speech)
        {
            lastSpeechAudioFormat = value;
        }
        
        SaveSettings();
    }
    
    partial void OnSeedValueChanged(int value)
    {
        SaveSettings();
        // Notify that DisplaySeedValue property has changed
        OnPropertyChanged(nameof(DisplaySeedValue));
    }
    
    // Method to upload a file to Wasabi cloud
    private async Task<string?> UploadToWasabiAsync(string filePath)
    {
        // Only upload in SFX mode and if WASABI is enabled
        if (GenerationType != GenerationType.SFX || !WASABI_ENABLED)
            return null;
            
        try
        {
            Debug.WriteLine($"Starting upload to Wasabi: {filePath}");
            return await _wasabiStorageService.UploadFileAsync(filePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to upload to Wasabi: {ex.Message}");
            throw new Exception($"Cloud upload failed: {ex.Message}", ex);
        }
    }

    private async Task InitializeVoicesAsync()
    {
        try
        {
            IsLoadingVoices = true;
            
            var voices = await _apiService.GetVoicesAsync();
            
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                AvailableVoices.Clear();
                foreach (var voice in voices.OrderBy(v => v.Name))
                {
                    AvailableVoices.Add(voice);
                }

                // Try to restore the last selected voice or use index 3
                string? lastVoiceId = null;
                try
                {
                    var settingsJson = _fileService.ReadTextFromFile(_fileService.GetSettingsFilePath());
                    if (!string.IsNullOrWhiteSpace(settingsJson))
                    {
                        var settings = JsonSerializer.Deserialize<AppSettings>(settingsJson);
                        lastVoiceId = settings?.LastSelectedVoiceId;
                    }
                }
                catch { }

                // First try to find the last used voice ID
                SelectedVoice = AvailableVoices.FirstOrDefault(v => v.Id == lastVoiceId);
                
                // If no last voice found, use voice at index 3 (or the last voice if fewer available)
                if (SelectedVoice == null)
                {
                    int indexToUse = Math.Min(DEFAULT_VOICE_INDEX, AvailableVoices.Count - 1);
                    if (indexToUse >= 0)
                    {
                        SelectedVoice = AvailableVoices.ElementAt(indexToUse);
                    }
                    else if (AvailableVoices.Any())
                    {
                        SelectedVoice = AvailableVoices.FirstOrDefault();
                    }
                }
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await _dialogService.ShowErrorDialogAsync("Error", $"Failed to load voices: {ex.Message}");
            });
        }
        finally
        {
            IsLoadingVoices = false;
        }
    }

    private async Task<string> GenerateSpeech(CancellationToken cancellationToken)
    {
        if (SelectedVoice == null)
        {
            throw new Exception("Please select a voice first");
        }

        // Get speech bytes from API service
        var speechBytes = await _apiService.GenerateSpeechAsync(
            PromptText,
            SelectedVoice.Id,
            SelectedModel,
            VoiceStability,
            VoiceSimilarity,
            VoiceExpression,
            SpeakerBoost,
            SelectedAudioFormat,
            SelectedQuality,
            cancellationToken);

        // Shorten and clean the prompt text for the filename
        string shortPrompt = _fileService.GetSafeFilename(PromptText.Length > 40 ? PromptText.Substring(0, 40) : PromptText);
        
        // Generate a more descriptive filename with voice name and model information
        var outputPath = Path.Combine(
            _fileService.GetOutputFolder(),
            $"Speech-{shortPrompt}-{GetQualityShortName()}_{DateTime.Now:yyyyMMddHHmmss}.{SelectedAudioFormat}"
        );

        await _fileService.WriteAllBytesAsync(outputPath, speechBytes);
        return outputPath;
    }

    private async Task<string> GenerateSFX(CancellationToken cancellationToken)
    {
        // Use the API service to generate SFX
        var sfxBytes = await _apiService.GenerateSFXAsync(
            PromptText,
            DurationSeconds,
            PromptInfluence,
            SeedValue > 0 ? SeedValue : null,
            SelectedQuality,
            cancellationToken);

        // Shorten and clean the prompt text for the filename
        string shortPrompt = _fileService.GetSafeFilename(PromptText);
        
        // Get current variation number from the progress text
        int currentVariation = 0;
        if (Variations > 1 && ProgressText.Contains("variation"))
        {
            try
            {
                var match = System.Text.RegularExpressions.Regex.Match(ProgressText, @"variation (\d+) of");
                if (match.Success && match.Groups.Count > 1)
                {
                    int.TryParse(match.Groups[1].Value, out currentVariation);
                }
            }
            catch
            {
                currentVariation = 0;
            }
        }
        
        // Generate filename with variation number if needed
        string variationSuffix = Variations > 1 ? $"-v{currentVariation}" : "";
        
        var outputPath = Path.Combine(
            _fileService.GetOutputFolder(),
            $"SFX-{shortPrompt}{variationSuffix}.mp3" // Always use .mp3 extension for SFX
        );

        await _fileService.WriteAllBytesAsync(outputPath, sfxBytes);
        return outputPath;
    }
    
    private string GetQualityShortName()
    {
        return SelectedQuality switch
        {
            "16kHz/16bit" => "std",
            "32kHz/24bit" => "high",
            "48kHz/24bit" => "max",
            _ => "std"
        };
    }

    private async Task<bool> ShowApiPreviewDialog()
    {
        if (string.IsNullOrWhiteSpace(PromptText))
        {
            await _dialogService.ShowErrorDialogAsync("Error", "Text input cannot be empty");
            return false;
        }

        // Create the appropriate API preview
        var apiPreview = GenerationType == GenerationType.Speech 
            ? CreateSpeechApiPreview() 
            : CreateSfxApiPreview();
            
        // Format the JSON for display
        var jsonForDisplay = JsonSerializer.Serialize(
            apiPreview,
            new JsonSerializerOptions { 
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            }
        );

        // Build the message with endpoint information
        var endpointInfo = GenerationType == GenerationType.Speech
            ? $"POST https://api.elevenlabs.io/v1/text-to-speech/{SelectedVoice?.Id}"
            : "POST https://api.elevenlabs.io/v1/sound-generation";
            
        return await _dialogService.ShowApiPreviewDialogAsync(endpointInfo, jsonForDisplay);
    }
    
    private object CreateSpeechApiPreview()
    {
        return new
        {
            text = PromptText,
            model_id = SelectedModel,
            voice_settings = new
            {
                stability = VoiceStability,
                similarity_boost = VoiceSimilarity,
                style = VoiceExpression,
                use_speaker_boost = SpeakerBoost
            },
            output_format = SelectedAudioFormat,
            quality = GetApiQualityValue(SelectedQuality)
        };
    }
    
    private object CreateSfxApiPreview()
    {
        int? seed = SeedValue > 0 ? SeedValue : null;

        return new
        {
            text = PromptText,
            duration_seconds = DurationSeconds,
            prompt_influence = PromptInfluence,
            output_format = "mp3", // Always show mp3 in the preview for SFX
            seed = seed,
            quality = GetApiQualityValue(SelectedQuality)
        };
    }
    
    private string GetApiQualityValue(string quality)
    {
        return quality switch
        {
            "16kHz/16bit" => "standard",
            "32kHz/24bit" => "high",
            "48kHz/24bit" => "maximum",
            _ => "standard"
        };
    }

    public async Task GenerateSoundEffects()
    {
        string? lastOutputPath = null;
        string? wasabiUrl = null;
        bool wasabiUploadFailed = false;
        string? wasabiErrorMessage = null;
        
        try
        {
            // Check API key first
            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                bool getApiKey = await _dialogService.ShowYesNoDialogAsync(
                    "API Key Required",
                    "You need an ElevenLabs API key to generate audio.\n\nWould you like to get a free API key now?");
                
                if (getApiKey)
                {
                    OpenApiKeyUrl();
                }
                
                return;
            }
            
            if (string.IsNullOrWhiteSpace(PromptText))
            {
                throw new Exception("Text input cannot be empty");
            }

            // Check current month's folder size before proceeding
            if (GenerationType == GenerationType.SFX && WASABI_ENABLED)
            {
                string currentMonthPrefix = DateTime.Now.ToString("yyyy/MM/");
                long totalSize = await _wasabiStorageService.GetFolderSizeAsync(currentMonthPrefix);
                double sizeInMB = totalSize / (1024.0 * 1024.0); // Convert bytes to MB

                bool proceed = await _dialogService.ShowYesNoDialogAsync(
                    "Storage Check",
                    $"Current month's storage usage: {sizeInMB:F2} MB\n\nDo you want to proceed with the generation?");

                if (!proceed)
                {
                    return;
                }
            }

            IsGenerating = true;
            ProgressValue = 0;
            _cancellationTokenSource = new CancellationTokenSource();

            int totalIterations = GenerationType == GenerationType.Speech ? 1 : Variations;
            
            // For SFX mode, we count generation as 80% of progress and upload as the final 20%
            // For Speech mode, generation is 100% of progress
            double generationProgressWeight = GenerationType == GenerationType.Speech ? 1.0 : 0.8;
            
            for (int i = 0; i < totalIterations; i++)
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    break;
                }

                ProgressText = totalIterations > 1 
                    ? $"Generating variation {i + 1} of {totalIterations}..."
                    : "Generating...";

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token);
                cts.CancelAfter(TimeSpan.FromMinutes(2)); // Timeout after 2 minutes

                if (GenerationType == GenerationType.Speech)
                {
                    lastOutputPath = await GenerateSpeech(cts.Token);
                    ProgressValue = 100; // In speech mode, we're done after generation
                }
                else
                {
                    lastOutputPath = await GenerateSFX(cts.Token);
                    
                    // In SFX mode, calculate the progress for this variation's generation
                    // Each variation gets equal portion of the total generation progress
                    double progressPerVariation = (generationProgressWeight / totalIterations) * 100;
                    
                    // Progress after generation is complete for this variation
                    double baseProgress = (i / (double)totalIterations) * generationProgressWeight * 100;
                    ProgressValue = baseProgress + progressPerVariation;
                    
                    // Upload each SFX variation to Wasabi as it's generated
                    if (lastOutputPath != null && GenerationType == GenerationType.SFX && WASABI_ENABLED)
                    {
                        try
                        {
                            ProgressText = "Finalizing...";
                            wasabiUrl = await UploadToWasabiAsync(lastOutputPath);
                            Debug.WriteLine($"Successfully finalized variation {i + 1} to Wasabi: {wasabiUrl}");
                        }
                        catch (Exception ex)
                        {
                            wasabiUploadFailed = true;
                            wasabiErrorMessage = ex.Message;
                            Debug.WriteLine($"Upload error for variation {i + 1}: {ex.Message}");
                        }
                    }
                }
            }

            if (_cancellationTokenSource.Token.IsCancellationRequested)
            {
                await _dialogService.ShowInfoDialogAsync("Cancelled", "Generation was cancelled.");
            }
            else
            {
                string successMessage = "Generation completed successfully!";
                
                // Only show Wasabi-related messages in debug logs, not to the user
                if (wasabiUploadFailed)
                {
                    Debug.WriteLine($"One or more Wasabi uploads failed: {wasabiErrorMessage}");
                }
                
                // Set final progress
                ProgressValue = 100;
                
                await _dialogService.ShowInfoDialogAsync("Success", successMessage);

                // Open the folder containing the last generated file
                if (lastOutputPath != null)
                {
                    _fileService.OpenFolder(Path.GetDirectoryName(lastOutputPath)!);
                }
            }
        }
        catch (OperationCanceledException)
        {
            await _dialogService.ShowInfoDialogAsync("Cancelled", "Generation was cancelled.");
        }
        catch (Exception ex)
        {
            string errorMessage = ex.Message;
            bool isApiKeyError = errorMessage.Contains("invalid_api_key") || 
                                errorMessage.Contains("Invalid API key") ||
                                errorMessage.Contains("Unauthorized");

            if (isApiKeyError)
            {
                bool getApiKey = await _dialogService.ShowYesNoDialogAsync(
                    "API Key Error",
                    "Your ElevenLabs API key is invalid or has expired.\n\nWould you like to get a free API key now?");
                
                if (getApiKey)
                {
                    OpenApiKeyUrl();
                }
            }
            else
            {
                // Show the regular error message, but don't mention Wasabi specifically
                string displayError = errorMessage;
                
                // If it's a cloud upload error, make it generic
                if (errorMessage.Contains("Wasabi"))
                {
                    displayError = "Failed to upload to cloud storage. Your file was saved locally.";
                    Debug.WriteLine($"Original error: {errorMessage}");
                }
                
                await _dialogService.ShowErrorDialogAsync("Error", $"Failed to generate: {displayError}");
            }
        }
        finally
        {
            IsGenerating = false;
            ProgressText = string.Empty;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private void OpenApiKeyUrl()
    {
        try
        {
            _browserService.OpenUrl(ELEVENLABS_SIGNUP_URL);
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await _dialogService.ShowErrorDialogAsync(
                    "Error",
                    $"Failed to open browser: {ex.Message}\n\nPlease visit {ELEVENLABS_SIGNUP_URL} manually.");
            });
        }
    }

    public void CancelGeneration()
    {
        _cancellationTokenSource?.Cancel();
    }

    [RelayCommand]
    private void ResetSettings()
    {
        try
        {
            // Delete settings file
            var settingsPath = _fileService.GetSettingsFilePath();
            if (_fileService.FileExists(settingsPath))
            {
                File.Delete(settingsPath);
            }

            // Reset to defaults
            DurationSeconds = 0.5;
            PromptInfluence = 0.3;
            Variations = 4;
            GenerationType = GenerationType.SFX;
            PromptText = string.Empty;
            VoiceStability = 0.5;
            VoiceSimilarity = 0.5;
            VoiceExpression = 0.0;
            SpeakerBoost = false;
            SelectedModel = "eleven_monolingual_v1";
            SelectedQuality = "16kHz/16bit";
            SelectedAudioFormat = "mp3";
            lastSpeechAudioFormat = "mp3";
            SeedValue = 0; // Reset seed to 0 (Random)
            
            // Set voice to index 3 if available
            if (AvailableVoices.Any())
            {
                int indexToUse = Math.Min(DEFAULT_VOICE_INDEX, AvailableVoices.Count - 1);
                if (indexToUse >= 0)
                {
                    SelectedVoice = AvailableVoices.ElementAt(indexToUse);
                }
                else
                {
                    SelectedVoice = AvailableVoices.FirstOrDefault();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to reset settings: {ex.Message}");
        }
    }
}
