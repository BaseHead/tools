using AISoundGenerator.ViewModels;

namespace AISoundGenerator.Models;

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
    public string? LastSpeechAudioFormat { get; set; } = "mp3";
    public int SeedValue { get; set; } = 0;
}