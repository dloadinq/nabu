using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using NanoWakeWord;
using Nabu.Local;
using Nabu.Local.Audio;
using Nabu.Local.Config;
using Nabu.Local.Detection;
using Nabu.Local.Hubs;
using Nabu.Local.Transcription;
using Nabu.Local.Vad;

var backendDetector = new WhisperBackendDetector();
backendDetector.AttachToWhisperLogs();

var builder = WebApplication.CreateSlimBuilder(args);

var url = builder.Configuration["WhisperLocal:Url"] ?? "http://localhost:50000";
builder.WebHost.UseUrls(url);

builder.Services.AddSignalR();
builder.Services.Configure<JsonHubProtocolOptions>(hubOptions =>
{
    hubOptions.PayloadSerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

builder.Services.Configure<WhisperLocalOptions>(builder.Configuration.GetSection(WhisperLocalOptions.SectionName));

if (!string.Equals(Directory.GetCurrentDirectory(), AppContext.BaseDirectory, StringComparison.Ordinal))
{
    Directory.SetCurrentDirectory(AppContext.BaseDirectory);
}

builder.Services.AddSingleton(backendDetector);
builder.Services.AddSingleton<IWhisperTranscriber>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<WhisperLocalOptions>>().Value;
    var logger = sp.GetRequiredService<ILogger<WhisperService>>();
    return new WhisperService(opts.Whisper.Language, opts.Whisper.GpuModelPath, opts.Whisper.CpuModelPath, logger);
});
builder.Services.AddScoped<IVadDetector>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<WhisperLocalOptions>>().Value;
    var vadOptions = opts.Vad;
    return new SileroVadDetectorAdapter(vadOptions.ModelPath, vadOptions.Threshold, vadOptions.SamplingRate, vadOptions.MinSpeechDurationMs,
        vadOptions.MaxSpeechDurationSeconds, vadOptions.MinSilenceDurationMs, vadOptions.SpeechPadMs);
});
builder.Services.AddSingleton(sp =>
{
    var opts = sp.GetRequiredService<IOptions<WhisperLocalOptions>>().Value;
    var wakeWordOptions = opts.WakeWord;
    return new WakeWordRuntime(new WakeWordRuntimeConfig
    {
        WakeWords = [new WakeWordConfig { Model = wakeWordOptions.Model, Threshold = wakeWordOptions.Threshold }],
        StepFrames = wakeWordOptions.StepFrames
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("WhisperLocalCors", policy => 
        policy
            .SetIsOriginAllowed(_ => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
        );
});

builder.Services.AddScoped<AudioProcessingPipeline>();

var app = builder.Build();

app.UseCors("WhisperLocalCors");

app.MapGet("/", (WhisperBackendDetector detector, IOptions<WhisperLocalOptions> opts) 
    => StatusPageHandler.GetStatusPage(detector, opts.Value));
app.MapGet("/blast", () => Results.Ok("Missed. Again."));
app.MapHub<WhisperHub>("/voiceHub");

var whisper = app.Services.GetRequiredService<IWhisperTranscriber>();
await whisper.EnsureInitializedAsync();

Console.Clear();
app.Run();

[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(byte[]))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
