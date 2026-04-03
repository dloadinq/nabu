using Nabu;

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
        if (string.Equals(text, NabuConstants.TerminationText, StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;

        _logger.LogInformation("[{Time}] Received: {Text}", DateTime.Now, text);

        return Task.CompletedTask;
    }
}
