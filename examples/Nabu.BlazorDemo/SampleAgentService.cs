namespace Nabu.BlazorDemo;

public class SampleAgentService : INabuHandler
{
    private ILogger<SampleAgentService> _logger;

    public SampleAgentService(ILogger<SampleAgentService> logger)
    {
        _logger = logger;
    }

    public event Func<string, Task>? OnTranscriptionReceived;

    public Task OnTranscriptionReadyAsync(string text)
    {
        _logger.LogInformation("[{Time}] Received: {Text}", DateTime.Now, text);
        return OnTranscriptionReceived?.Invoke(text) ?? Task.CompletedTask;
    }
}
