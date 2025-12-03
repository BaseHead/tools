using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AISoundGenerator.Models;

namespace AISoundGenerator.Services;

public interface IApiService
{
    void SetApiKey(string apiKey);
    Task<List<VoiceModel>> GetVoicesAsync();
    Task<byte[]> GenerateSpeechAsync(string text, string voiceId, string modelId, double stability, 
        double similarity, double expression, bool speakerBoost, string audioFormat, string quality, 
        CancellationToken cancellationToken);
    Task<byte[]> GenerateSFXAsync(string text, double durationSeconds, double promptInfluence, 
        int? seed, string quality, CancellationToken cancellationToken);
}