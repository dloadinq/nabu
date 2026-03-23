using Nabu.RCL;

namespace Nabu.RazorPagesDemo;

public class WhisperAgentService : IWhisperHandler
{
    private readonly ILogger<WhisperAgentService> _logger;

    public WhisperAgentService(ILogger<WhisperAgentService> logger)
    {
        _logger = logger;
    }

    public Task OnTranscriptionReadyAsync(string text)
    {
        if (string.Equals(text, WhisperConstants.TerminationText, StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;

        _logger.LogInformation("WhisperAgentService received: {Text}", text);

        return Task.CompletedTask;
    }
}
