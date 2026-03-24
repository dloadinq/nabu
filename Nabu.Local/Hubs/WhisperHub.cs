using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Nabu.Core.Audio;
using Nabu.Core.Transcription;

namespace Nabu.Local.Hubs;

public class WhisperHub : Hub
{
    private readonly ILogger<WhisperHub> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<WhisperHub> _hubContext;
    private static readonly ConcurrentDictionary<string, HubPipelineSession> Sessions = new();

    public WhisperHub(ILogger<WhisperHub> logger, IServiceProvider serviceProvider, IHubContext<WhisperHub> hubContext)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
    }

    public override async Task OnConnectedAsync()
    {
        var connectionId = Context.ConnectionId;
        _logger.LogInformation("Client connected: {ConnectionId}", connectionId);

        var scope = _serviceProvider.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<AudioProcessingPipeline>();
        var session = new HubPipelineSession(pipeline, scope);

        session.WakeWordHandler = (word) => SafeSendAsync(connectionId, "OnWakeWordDetected", word);
        session.PreviewHandler = (text) => SafeSendAsync(connectionId, "OnTranscriptionPreview", text);
        session.FinalHandler = (text) => SafeSendAsync(connectionId, "OnTranscriptionFinal", text);
        session.StatusHandler = (status) => SafeSendAsync(connectionId, "OnStatusChanged", status);

        pipeline.OnWakeWordDetected += session.WakeWordHandler;
        pipeline.OnTranscriptionPreview += session.PreviewHandler;
        pipeline.OnTranscriptionFinal += session.FinalHandler;
        pipeline.OnStatusChanged += session.StatusHandler;

        Sessions[connectionId] = session;
        await base.OnConnectedAsync();
    }

    private async void SafeSendAsync(string connectionId, string method, string arg)
    {
        try
        {
            await _hubContext.Clients.Client(connectionId).SendAsync(method, arg);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send {Method} to connection {ConnectionId}", method, connectionId);
        }
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
        
        _logger.LogInformation("Client disconnected: {ConnectionId}", connectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendAudioChunk(string base64Data)
    {
        var connectionId = Context.ConnectionId;
        if (!Sessions.TryGetValue(connectionId, out var session))
        {
            _logger.LogWarning("SendAudioChunk: No session for connection {ConnectionId}. Active sessions: {Count}. Audio may not be processed.", connectionId, Sessions.Count);
            return;
        }
        byte[] data;
        try
        {
            data = Convert.FromBase64String(base64Data);
        }
        catch (FormatException ex)
        {
            _logger.LogWarning(ex, "SendAudioChunk: Invalid base64 data from connection {ConnectionId}", connectionId);
            throw new HubException("Invalid audio data format.");
        }
        try
        {
            await session.Pipeline.ProcessAudioChunkAsync(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process audio chunk for connection {ConnectionId}", connectionId);
            throw new HubException("Server failed to process audio chunk.");
        }
    }

    public Task SetLanguage(string language)
    {
        var connectionId = Context.ConnectionId;
        _logger.LogInformation("Client {ConnectionId} set language to: {Language}", connectionId, language);
        if (Sessions.TryGetValue(connectionId, out var session))
        {
            session.Pipeline.SetPreferredLanguage(language);
        }
        return Task.CompletedTask;
    }

    public async Task FinishRecording()
    {
        var connectionId = Context.ConnectionId;
        if (!Sessions.TryGetValue(connectionId, out var session))
        {
            _logger.LogWarning("FinishRecording: No session for connection {ConnectionId}", connectionId);
            return;
        }
        await session.Pipeline.ForceStopAndFinalizeAsync();
    }

    private sealed class HubPipelineSession
    {
        public HubPipelineSession(AudioProcessingPipeline pipeline, IServiceScope scope)
        {
            Pipeline = pipeline;
            Scope = scope;
        }

        public AudioProcessingPipeline Pipeline { get; }
        public IServiceScope Scope { get; }
        public Action<string> WakeWordHandler { get; set; } = _ => { };
        public Action<string> PreviewHandler { get; set; } = _ => { };
        public Action<string> FinalHandler { get; set; } = _ => { };
        public Action<string> StatusHandler { get; set; } = _ => { };
    }
}
