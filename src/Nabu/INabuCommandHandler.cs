namespace Nabu;

/// <summary>Implement this interface to receive resolved commands from the NabuAssistant widget.</summary>
public interface INabuCommandHandler
{
    /// <summary>Called when a spoken phrase resolves to a registered command.</summary>
    /// <param name="commandId">The command identifier as registered via <c>AddCommand</c>.</param>
    /// <param name="originalText">The original transcription in the user's language.</param>
    Task OnCommandAsync(string commandId, string originalText);
}