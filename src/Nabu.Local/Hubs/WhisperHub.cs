using System.Buffers;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Nabu.Core.Audio;
using Nabu.Inference.Embeddings;

namespace Nabu.Local.Hubs;

public class WhisperHub(
    ILogger<WhisperHub> logger,
    IServiceProvider serviceProvider,
    IHubContext<WhisperHub> hubContext,
    CommandSyncService? commandSyncService = null)
    : Hub
{
    private static readonly ConcurrentDictionary<string, HubPipelineSession> Sessions = new();

    public override async Task OnConnectedAsync()
    {
        var connectionId = Context.ConnectionId;
        logger.LogInformation("Client connected: {ConnectionId}", connectionId);

        var origin = Context.GetHttpContext()?.Request.Headers.Origin.ToString() ?? "unknown";
        var collectionName = CommandSyncService.CollectionName(origin);

        var scope = serviceProvider.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<AudioProcessingPipeline>();
        var session = new HubPipelineSession(pipeline, scope, collectionName);

        session.WakeWordHandler = word => _ = SafeSendAsync(connectionId, "OnWakeWordDetected", word);
        session.PreviewHandler = text => _ = SafeSendAsync(connectionId, "OnTranscriptionPreview", text);
        session.StatusHandler = status => _ = SafeSendAsync(connectionId, "OnStatusChanged", status);
        session.FinalHandler = (text, translatedText) =>
        {
            _ = SafeSendAsync(connectionId, "OnTranscriptionFinal", text);

            if (commandSyncService is null) return;
            if (commandSyncService.Resolve(session.CommandCollectionName, translatedText ?? text, session.CurrentRoute)
                is { } commandId)
                _ = SafeSendCore(connectionId, "OnCommandResolved", commandId, translatedText ?? text);
        };

        pipeline.OnWakeWordDetected += session.WakeWordHandler;
        pipeline.OnTranscriptionPreview += session.PreviewHandler;
        pipeline.OnTranscriptionFinal += session.FinalHandler;
        pipeline.OnStatusChanged += session.StatusHandler;

        Sessions[connectionId] = session;
        await base.OnConnectedAsync();
    }

    private Task SafeSendAsync(string connectionId, string method, string arg)
    {
        return hubContext.Clients.Client(connectionId).SendAsync(method, arg)
            .ContinueWith(
                t => logger.LogWarning(t.Exception?.GetBaseException(), "Failed to send {Method} to {ConnectionId}",
                    method, connectionId),
                TaskContinuationOptions.OnlyOnFaulted);
    }

    private Task SafeSendCore(string connectionId, string method, params object?[] args)
    {
        return hubContext.Clients.Client(connectionId).SendCoreAsync(method, args)
            .ContinueWith(
                t => logger.LogWarning(t.Exception?.GetBaseException(), "Failed to send {Method} to {ConnectionId}",
                    method, connectionId),
                TaskContinuationOptions.OnlyOnFaulted);
    }
    
    public async Task SendAudioChunk(string base64Data)
    {
        var connectionId = Context.ConnectionId;
        if (!Sessions.TryGetValue(connectionId, out var session)) return;
        if (string.IsNullOrWhiteSpace(base64Data)) return;

        var maxByteCount = (base64Data.Length * 3 + 3) / 4;
        var rented = ArrayPool<byte>.Shared.Rent(maxByteCount);

        if (Convert.TryFromBase64String(base64Data, rented, out var bytesWritten))
        {
            try
            {
                await session.Pipeline.ProcessAudioChunkAsync(rented, bytesWritten);
            }
            catch (Exception ex)
            {
                ArrayPool<byte>.Shared.Return(rented);
                logger.LogError(ex, "Failed to delegate audio chunk for {ConnectionId}", connectionId);
            }
        }
        else
        {
            ArrayPool<byte>.Shared.Return(rented);
            logger.LogWarning("Invalid Base64 received from {ConnectionId}", connectionId);
        }
    }

    public Task SetLanguage(string language)
    {
        var connectionId = Context.ConnectionId;
        logger.LogInformation("Client {ConnectionId} set language: {Language}", connectionId, language);
        if (Sessions.TryGetValue(connectionId, out var session)) session.Pipeline.SetPreferredLanguage(language);
        return Task.CompletedTask;
    }

    public Task CancelRecording()
    {
        if (!Sessions.TryGetValue(Context.ConnectionId, out var session))
        {
            logger.LogWarning("CancelRecording: No session for {ConnectionId}", Context.ConnectionId);
            return Task.CompletedTask;
        }

        logger.LogInformation("CancelRecording: discarding for {ConnectionId}", Context.ConnectionId);
        return session.Pipeline.CancelAndDiscardAsync();
    }

    public string[] GetCommandHashes()
    {
        return commandSyncService is not null && Sessions.TryGetValue(Context.ConnectionId, out var session)
            ? commandSyncService.GetHashes(session.CommandCollectionName)
            : [];
    }

    public Task PatchCommands(CommandSyncItem[] upsertItems, string[] retainedHashes)
    {
        if (commandSyncService is null || !Sessions.TryGetValue(Context.ConnectionId, out var session))
            return Task.CompletedTask;
        return commandSyncService.PatchAsync(session.CommandCollectionName, upsertItems, retainedHashes);
    }
    
    public Task SetPageContext(string route)
    {
        if (Sessions.TryGetValue(Context.ConnectionId, out var session))
            session.CurrentRoute = route;
        return Task.CompletedTask;
    }
    
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        if (Sessions.TryRemove(connectionId, out var session))
        {
            session.Pipeline.OnWakeWordDetected -= session.WakeWordHandler;
            session.Pipeline.OnTranscriptionPreview -= session.PreviewHandler;
            session.Pipeline.OnTranscriptionFinal -= session.FinalHandler;
            session.Pipeline.OnStatusChanged -= session.StatusHandler;

            session.Scope.Dispose();
        }

        logger.LogInformation("Client disconnected: {ConnectionId}", connectionId);
        await base.OnDisconnectedAsync(exception);
    }

    private sealed class HubPipelineSession(
        AudioProcessingPipeline pipeline,
        IServiceScope scope,
        string commandCollectionName)
    {
        public AudioProcessingPipeline Pipeline { get; } = pipeline;
        public IServiceScope Scope { get; } = scope;
        public string CommandCollectionName { get; } = commandCollectionName;
        public string? CurrentRoute { get; set; }

        public Action<string> WakeWordHandler { get; set; } = _ => { };
        public Action<string> PreviewHandler { get; set; } = _ => { };
        public Action<string, string?> FinalHandler { get; set; } = (_, _) => { };
        public Action<string> StatusHandler { get; set; } = _ => { };
    }
}