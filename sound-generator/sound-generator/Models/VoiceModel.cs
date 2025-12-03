using System.Text.Json.Serialization;

namespace AISoundGenerator.Models;

public class VoiceModel
{
    [JsonPropertyName("voice_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("preview_url")]
    public string PreviewUrl { get; set; } = string.Empty;

    [JsonPropertyName("labels")]
    public VoiceLabels Labels { get; set; } = new();

    public override string ToString() => $"{Name} ({Labels.Accent} - {Labels.Description})";
}

public class VoiceLabels
{
    [JsonPropertyName("accent")]
    public string Accent { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("age")]
    public string Age { get; set; } = string.Empty;

    [JsonPropertyName("gender")]
    public string Gender { get; set; } = string.Empty;

    [JsonPropertyName("use_case")]
    public string UseCase { get; set; } = string.Empty;
}