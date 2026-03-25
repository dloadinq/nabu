using Nabu.RCL;

namespace Nabu.RazorPagesDemo;

public class SampleAgentService : INabuHandler
{
    private readonly ILogger<SampleAgentService> _logger;

    public SampleAgentService(ILogger<SampleAgentService> logger)
    {
        _logger = logger;
    }

    public Task OnTranscriptionReadyAsync(string text)
    {
        if (string.Equals(text, WhisperConstants.TerminationText, StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;

        _logger.LogInformation("SampleAgentService received: {Text}", text);

        return Task.CompletedTask;
    }
}
