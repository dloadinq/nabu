namespace Nabu;

/// <summary>
/// Internal <see cref="INabuCommandHandler"/> that forwards resolved commands to the scoped
/// <see cref="VoiceCommandRegistry"/>. Registered automatically by <see cref="NabuBuilder"/>.
/// </summary>
internal sealed class VoiceRegistryCommandHandler(VoiceCommandRegistry registry) : INabuCommandHandler
{
    /// <inheritdoc/>
    public Task OnCommandAsync(string commandId, string text)
        => registry.TryExecuteAsync(commandId, text);
}
