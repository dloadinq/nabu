using Nabu.RCL;

namespace Nabu.BlazorDemo;

public class WhisperAgentService : IWhisperHandler
{
    public event Func<string, Task>? OnTranscriptionReceived;

    public Task OnTranscriptionReadyAsync(string text)
    {
        return OnTranscriptionReceived?.Invoke(text) ?? Task.CompletedTask;
    }
}
