using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Nabu.Core.Config;
using Nabu.Core.Hardware;
using Nabu.Core.Models;
using Nabu.Core.ModelSetup;
using Nabu.Inference.Embeddings;
using Nabu.Inference.Transcription;
using Nabu.Local;
using Nabu.Local.Hubs;

if (!string.Equals(Directory.GetCurrentDirectory(), AppContext.BaseDirectory, StringComparison.Ordinal))
    Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var builder = WebApplication.CreateSlimBuilder(args);

var url = builder.Configuration["NabuLocal:Url"] ?? "http://localhost:50000";
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

builder.Services.Configure<NabuLocalOptions>(builder.Configuration.GetSection(NabuLocalOptions.SectionName));
builder.Services.Configure<EmbeddingOptions>(builder.Configuration.GetSection(EmbeddingOptions.SectionName));

var nabuOptions = builder.Configuration.GetSection(NabuLocalOptions.SectionName).Get<NabuLocalOptions>() ?? new NabuLocalOptions();
var modelsDirectory = nabuOptions.Whisper.ModelsDirectory;
var language = nabuOptions.Whisper.Language;

Console.Clear();
var gpu = ModelManager.DetectGpu();

var selection = ModelManager.PromptModelSize(modelsDirectory, gpu);
if (selection is null) return;
var modelSize = selection.Size;
var forceCpu = selection.ForceCpu;

var (resolvedModelPath, loadedModel) = await ModelManager.EnsureModelAsync(modelSize, modelsDirectory, gpu, forceCpu);

Console.Clear();
Console.WriteLine($"Model: {loadedModel.DisplayName}");

builder.Services.AddSingleton(gpu);
builder.Services.AddSingleton(loadedModel);
builder.Services.AddAudioServices(language, resolvedModelPath);

var embeddingOptions = builder.Configuration.GetSection(EmbeddingOptions.SectionName).Get<EmbeddingOptions>() ?? new EmbeddingOptions();

if (embeddingOptions.Enabled && await EmbeddingModelSetup.EnsureDownloadedAsync(embeddingOptions.ModelDirectory))
{
    var modelPath = Path.Combine(embeddingOptions.ModelDirectory, EmbeddingModelSetup.ModelFileName);
    var vocabPath = Path.Combine(embeddingOptions.ModelDirectory, EmbeddingModelSetup.VocabFileName);

    builder.Services.AddSingleton(_ => new SentenceEmbedder(modelPath, vocabPath));
    builder.Services.AddSingleton(_ => new CommandStore(embeddingOptions.CommandDirectory));
    builder.Services.AddSingleton<CommandSyncService>();
}
else
{
    Console.WriteLine("Embeddings Disabled (set Embedding:Enabled = true and ensure model files).");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("NabuLocalCors", policy =>
        policy
            .SetIsOriginAllowed(_ => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
    );
});

var app = builder.Build();

app.UseCors("NabuLocalCors");

var statusHtml = StatusPageHandler.BuildStatusPage(
    app.Services.GetRequiredService<GpuInfo>(),
    app.Services.GetRequiredService<LoadedModelInfo>(),
    app.Services.GetRequiredService<IOptions<NabuLocalOptions>>().Value);

app.MapGet("/", () => Results.Content(statusHtml, "text/html"));
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
    catch (OperationCanceledException)
    {
    }
}

[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(CommandSyncItem))]
[JsonSerializable(typeof(CommandSyncItem[]))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}