namespace Nabu.Inference.Embeddings;

/// <summary>
/// Represents a single command phrase sent from the Blazor client to the local server during
/// an incremental embedding sync. New or changed phrases are included here; unchanged phrases
/// are represented only by their hash in the <c>retainedHashes</c> array.
/// </summary>
/// <param name="CommandId">The command identifier this phrase belongs to.</param>
/// <param name="Text">The natural language phrase to embed.</param>
/// <param name="Hash">Content hash that uniquely identifies this phrase/scope combination.</param>
/// <param name="Scope">Optional route scope restricting when this phrase is active.</param>
/// <param name="ExcludeScope">Optional route to suppress this phrase on.</param>
public sealed record CommandSyncItem(string CommandId, string Text, string Hash, string? Scope = null, string? ExcludeScope = null);
