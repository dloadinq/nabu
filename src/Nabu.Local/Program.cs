using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Nabu.Core.Config;
using Nabu.Local.Hubs;
using Nabu.Core.Hardware;
using Nabu.Core.Models;
using Nabu.Core.ModelSetup;
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

var gpu = ModelManager.DetectGpu();

var forceCpu = false;
if (string.IsNullOrWhiteSpace(modelSize))
{
    var selection = ModelManager.PromptModelSize(modelsDirectory, gpu);
    if (selection is null) return;
    modelSize = selection.Size;
    forceCpu = selection.ForceCpu;
}

var (resolvedModelPath, loadedModel) = await ModelManager.EnsureModelAsync(modelSize, modelsDirectory, gpu, forceCpu);

Console.Clear();
Console.WriteLine($"Model: {loadedModel.DisplayName}");

builder.Services.AddSingleton(gpu);
builder.Services.AddSingleton(loadedModel);
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

app.MapGet("/", (GpuInfo gpuInfo, LoadedModelInfo loadedModel, IOptions<WhisperLocalOptions> opts)
    => StatusPageHandler.GetStatusPage(gpuInfo, loadedModel, opts.Value));
app.MapGet("/blast", () => Results.Ok("Missed. Again."));
app.MapHub<WhisperHub>("/voiceHub");

var whisper = app.Services.GetRequiredService<IWhisperTranscriber>();

using var cts = new CancellationTokenSource();
var animTask = AnimateInitAsync(cts.Token);
await whisper.EnsureInitializedAsync();
await cts.CancelAsync();
await animTask;

Console.Clear();
app.Run();

static async Task AnimateInitAsync(CancellationToken ct)
{
    var frames = new[] { ".  ", ".. ", "..." };
    var row = Console.CursorTop;
    var i = 0;
    try
    {
        while (!ct.IsCancellationRequested)
        {
            Console.SetCursorPosition(0, row);
            Console.Write($"Initializing model{frames[i++ % 3]}");
            await Task.Delay(400, ct);
        }
    }
    catch (OperationCanceledException) { }
}

[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(byte[]))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}