namespace Nabu.Inference.Embeddings;

/// <summary>
/// Persisted record in the embedding store that pairs a command registration with its pre-computed
/// sentence embedding vector. Stored as JSON in the command store directory.
/// </summary>
public sealed class CommandEntry
{
    /// <summary>The command identifier this phrase belongs to.</summary>
    public string CommandId { get; set; } = "";

    /// <summary>The natural language phrase that was embedded.</summary>
    public string Text { get; set; } = "";

    /// <summary>Content hash used to detect stale entries during incremental sync.</summary>
    public string Hash { get; set; } = "";

    /// <summary>Optional route scope that restricts when this phrase is eligible for matching.</summary>
    public string? Scope { get; set; }

    /// <summary>Optional route to suppress this phrase on.</summary>
    public string? ExcludeScope { get; set; }

    /// <summary>The 384-dimensional sentence embedding vector produced by the all-MiniLM-L6-v2 model.</summary>
    public float[] Vector { get; set; } = [];
}
