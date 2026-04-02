namespace Nabu.Inference.Embeddings;

/// <summary>
/// Configuration options for the sentence embedding command resolver.
/// Bound from the <c>"Embedding"</c> section in <c>appsettings.json</c>.
/// </summary>
public sealed class EmbeddingOptions
{
    /// <summary>The configuration section name used for binding.</summary>
    public const string SectionName = "Embedding";

    /// <summary>Directory where the <c>all-MiniLM-L6-v2</c> ONNX model and vocabulary file are stored.</summary>
    public string ModelDirectory { get; set; } = "resources/embedding";

    /// <summary>Directory where per-origin command embedding JSON collections are persisted.</summary>
    public string CommandDirectory { get; set; } = "resources/commands";

    /// <summary>
    /// Minimum cosine similarity score for a match to be accepted. Utterances whose best match
    /// falls below this value are rejected as unrecognised commands.
    /// </summary>
    public float Threshold { get; set; } = 0.55f;

    /// <summary>
    /// Minimum difference between the top-1 and top-2 cosine scores required when the top score
    /// is below <see cref="HighConfidenceThreshold"/>. Prevents ambiguous, low-margin matches.
    /// </summary>
    public float MinConfidenceGap { get; set; } = 0.05f;

    /// <summary>
    /// When the top score meets or exceeds this value, the gap check is skipped entirely.
    /// A score this high indicates a near-perfect, unambiguous match.
    /// </summary>
    public float HighConfidenceThreshold { get; set; } = 0.85f;

    /// <summary>
    /// When <c>false</c>, the embedding-based command resolver is disabled and no commands are resolved
    /// on the server side.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
