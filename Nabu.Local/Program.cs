using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Nabu.Core.Config;
using Nabu.Local.Hubs;
using Nabu.Core.Models;
using Nabu.Core.Transcription;
using Nabu.Local;

if (!string.Equals(Directory.GetCurrentDirectory(), AppContext.BaseDirectory, StringComparison.Ordinal))
    Directory.SetCurrentDirectory(AppContext.BaseDirectory);

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

var whisperConfig = builder.Configuration.GetSection("WhisperLocal:Whisper");
var modelSize = whisperConfig["ModelSize"] ?? "";
var modelsDirectory = whisperConfig["ModelsDirectory"] ?? "models";
var language = whisperConfig["Language"] ?? "english";

if (string.IsNullOrWhiteSpace(modelSize))
    modelSize = ModelManager.PromptModelSize(modelsDirectory);

var gpu = ModelManager.DetectGpu();
var resolvedModelPath = await ModelManager.EnsureModelAsync(modelSize, modelsDirectory);

builder.Services.AddSingleton(gpu);
builder.Services.AddAudioServices(language, resolvedModelPath);

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

var app = builder.Build();

app.UseCors("WhisperLocalCors");

app.MapGet("/", (GpuInfo gpuInfo, IOptions<WhisperLocalOptions> opts)
    => StatusPageHandler.GetStatusPage(gpuInfo, opts.Value));
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