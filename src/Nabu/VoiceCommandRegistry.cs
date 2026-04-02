namespace Nabu;

/// <summary>
/// Per-circuit registry for dispatching resolved voice commands to page-level handlers.
/// Register a command on a page, unregister it when the page is disposed.
/// </summary>
public sealed class VoiceCommandRegistry
{
    private readonly Dictionary<string, Func<string, Task>> _callbacks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a callback for the given command identifier, replacing any previously registered callback
    /// for the same identifier. Typically called from a page's <c>OnInitialized</c> lifecycle method.
    /// </summary>
    /// <param name="commandId">The command identifier to listen for.</param>
    /// <param name="callback">
    /// Async delegate invoked when the command fires. Receives the original transcription text.
    /// </param>
    public void Register(string commandId, Func<string, Task> callback)
    {
        _callbacks[commandId] = callback;
    }

    /// <summary>
    /// Removes the callback for the given command identifier. Should be called from a page's
    /// <c>Dispose</c> method to prevent stale references.
    /// </summary>
    /// <param name="commandId">The command identifier to deregister.</param>
    public void Unregister(string commandId)
    {
        _callbacks.Remove(commandId);
    }

    /// <summary>
    /// Invokes the registered callback for <paramref name="commandId"/> if one exists; otherwise returns
    /// a completed task. Called by the framework. Not intended for direct use.
    /// </summary>
    /// <param name="commandId">The resolved command identifier.</param>
    /// <param name="text">The original transcription text to pass to the callback.</param>
    internal Task TryExecuteAsync(string commandId, string text)
    {
        return _callbacks.TryGetValue(commandId, out var callback) ? callback(text) : Task.CompletedTask;
    }
}
