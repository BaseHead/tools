using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AISoundGenerator.Models;
using System.Diagnostics;

namespace AISoundGenerator.Services;

public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;

    public ApiService()
    {
        _httpClient = new HttpClient();
    }

    public void SetApiKey(string apiKey)
    {
        _httpClient.DefaultRequestHeaders.Remove("xi-api-key");
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("xi-api-key", apiKey);
        }
    }

    public async Task<List<VoiceModel>> GetVoicesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("https://api.elevenlabs.io/v1/voices");
            
            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                var voicesResponse = JsonSerializer.Deserialize<VoicesResponse>(jsonString, options);
                return voicesResponse?.Voices ?? new List<VoiceModel>();
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to load voices: {errorContent}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"API error: {ex.Message}");
            throw;
        }
    }

    public async Task<byte[]> GenerateSpeechAsync(
        string text, 
        string voiceId,
        string modelId,
        double stability,
        double similarity,
        double expression,
        bool speakerBoost,
        string audioFormat,
        string quality,
        CancellationToken cancellationToken)
    {
        var qualityValue = GetQualityValue(quality);

        var requestBody = new
        {
            text = text,
            model_id = modelId,
            voice_settings = new
            {
                stability = stability,
                similarity_boost = similarity,
                style = expression,
                use_speaker_boost = speakerBoost
            },
            output_format = audioFormat,
            quality = qualityValue
        };

        var jsonString = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(jsonString, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(
            $"https://api.elevenlabs.io/v1/text-to-speech/{voiceId}",
            content,
            cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to generate speech: {errorContent}");
        }

        return await response.Content.ReadAsByteArrayAsync();
    }

    public async Task<byte[]> GenerateSFXAsync(
        string text,
        double durationSeconds,
        double promptInfluence,
        int? seed,
        string quality,
        CancellationToken cancellationToken)
    {
        var qualityValue = GetQualityValue(quality);

        var requestBody = new
        {
            text = text,
            duration_seconds = durationSeconds,
            prompt_influence = promptInfluence,
            output_format = "mp3", // Always use MP3 for SFX as it's the only supported format
            seed = seed, // Will be null for seed value 0 (random)
            quality = qualityValue
        };

        var jsonString = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(jsonString, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(
            "https://api.elevenlabs.io/v1/sound-generation",
            content,
            cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to generate sound effect: {errorContent}");
        }

        return await response.Content.ReadAsByteArrayAsync();
    }

    private string GetQualityValue(string selectedQuality)
    {
        return selectedQuality switch
        {
            "16kHz/16bit" => "standard",
            "32kHz/24bit" => "high",
            "48kHz/24bit" => "maximum",
            _ => "standard"
        };
    }
}