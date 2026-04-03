using Nabu;

namespace Nabu.BlazorDemoSimple;

public class SampleAgentService : INabuHandler
{
    public event Func<string, Task>? OnTranscriptionReceived;

    public Task OnTranscriptionReadyAsync(string text)
    {
        return OnTranscriptionReceived?.Invoke(text) ?? Task.CompletedTask;
    }
}
