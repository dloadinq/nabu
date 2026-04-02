using Microsoft.Extensions.Options;
using NanoWakeWord;
using Nabu.Core.Audio;
using Nabu.Core.Config;
using Nabu.Core.Kws;
using Nabu.Core.Transcription;
using Nabu.Core.Vad;
using Nabu.Inference.Kws;
using Nabu.Inference.Transcription;
using Nabu.Inference.Vad;

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
            var vad = sp.GetRequiredService<IOptions<NabuLocalOptions>>().Value.Vad;
            return new SileroVadDetectorAdapter(vad.ModelPath, vad.SamplingRate);
        });
        
        services.AddScoped<IWakeWordDetector>(sp =>
        {
            var wakeWord = sp.GetRequiredService<IOptions<NabuLocalOptions>>().Value.WakeWord;
            var runtime = new WakeWordRuntime(new WakeWordRuntimeConfig
            {
                WakeWords = [new WakeWordConfig { Model = wakeWord.Model, Threshold = wakeWord.Threshold }],
                StepFrames = wakeWord.StepFrames
            });
            return new WakeWordDetector(runtime);
        });

        services.AddScoped<AudioProcessingPipeline>();

        return services;
    }
}
