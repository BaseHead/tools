using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AISoundGenerator.Models;

public class VoicesResponse
{
    [JsonPropertyName("voices")]
    public List<VoiceModel> Voices { get; set; } = new();
}