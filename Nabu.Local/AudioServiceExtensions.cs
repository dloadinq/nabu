using Microsoft.Extensions.Options;
using NanoWakeWord;
using Nabu.Core.Audio;
using Nabu.Core.Config;
using Nabu.Core.Transcription;
using Nabu.Core.Vad;

namespace Nabu.Local;

internal static class AudioServiceExtensions
{
    public static IServiceCollection AddAudioServices(
        this IServiceCollection services,
        string language,
        string modelPath)
    {
        services.AddSingleton<IWhisperTranscriber>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<WhisperService>>();
            return new WhisperService(language, modelPath, logger);
        });

        services.AddScoped<IVadDetector>(sp =>
        {
            var vad = sp.GetRequiredService<IOptions<WhisperLocalOptions>>().Value.Vad;
            return new SileroVadDetectorAdapter(
                vad.ModelPath,
                vad.Threshold,
                vad.SamplingRate,
                vad.MinSpeechDurationMs,
                vad.MaxSpeechDurationSeconds,
                vad.MinSilenceDurationMs,
                vad.SpeechPadMs);
        });

        services.AddSingleton(sp =>
        {
            var wakeWord = sp.GetRequiredService<IOptions<WhisperLocalOptions>>().Value.WakeWord;
            return new WakeWordRuntime(new WakeWordRuntimeConfig
            {
                WakeWords  = [new WakeWordConfig { Model = wakeWord.Model, Threshold = wakeWord.Threshold }],
                StepFrames = wakeWord.StepFrames
            });
        });

        services.AddScoped<AudioProcessingPipeline>();

        return services;
    }
}
