using Nabu;

namespace Nabu.BlazorDemo;

public class SampleAgentService : INabuHandler
{
    public event Func<string, Task>? OnTranscriptionReceived;

    public Task OnTranscriptionReadyAsync(string text)
    {
        return OnTranscriptionReceived?.Invoke(text) ?? Task.CompletedTask;
    }
}
