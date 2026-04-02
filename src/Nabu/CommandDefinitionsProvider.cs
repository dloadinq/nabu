using System.Security.Cryptography;
using System.Text;

namespace Nabu;

/// <summary>
/// Holds the complete set of <see cref="CommandDefinitionItem"/> instances registered at startup.
/// Injected as a singleton so the Nabu widget can read command definitions across the application lifetime.
/// </summary>
internal sealed class CommandDefinitionsProvider
{
    /// <summary>Gets all registered command definition items.</summary>
    public IReadOnlyList<CommandDefinitionItem> Items { get; }

    internal CommandDefinitionsProvider(List<CommandDefinitionItem> items)
    {
        Items = items;
    }

    /// <summary>
    /// Computes a deterministic SHA-256 hex hash for a command definition, used to detect changes
    /// between the application's registered commands and the persisted embedding store.
    /// </summary>
    /// <param name="commandId">The command identifier.</param>
    /// <param name="text">The natural language phrase associated with the command.</param>
    /// <param name="scope">Optional route scope that restricts when the command is active.</param>
    /// <param name="excludeScope">Optional route to explicitly exclude from this command's scope.</param>
    /// <returns>A uppercase hex string of the SHA-256 digest.</returns>
    internal static string Hash(string commandId, string text, string? scope = null, string? excludeScope = null)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{commandId}:{text}:{scope ?? ""}:{excludeScope ?? ""}")));
    }
}

/// <summary>
/// Represents a single natural language phrase registered for a voice command, together with its
/// routing scope and a content hash used for incremental embedding synchronisation.
/// </summary>
/// <param name="CommandId">The unique identifier of the command this phrase belongs to.</param>
/// <param name="Text">The natural language phrase that should trigger the command.</param>
/// <param name="Hash">SHA-256 content hash computed over the combination of all fields.</param>
/// <param name="Scope">
/// Optional route prefix. When set, this phrase is only eligible when the current route starts with the value.
/// </param>
/// <param name="ExcludeScope">
/// Optional route to suppress this phrase on. Useful for navigation commands that should not fire
/// while already on the target page.
/// </param>
public sealed record CommandDefinitionItem(string CommandId, string Text, string Hash, string? Scope = null, string? ExcludeScope = null);