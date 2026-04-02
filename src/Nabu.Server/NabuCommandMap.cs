using SmartComponents.LocalEmbeddings;

namespace Nabu.Server;

/// <summary>
/// Maps commands to natural language descriptions using local embeddings.
/// Resolves spoken phrases (translated to English) to command IDs via semantic similarity.
/// No keyword lists required, synonyms and paraphrases are handled automatically.
/// </summary>
public sealed class NabuCommandMap : IDisposable
{
    private readonly LocalEmbedder _embedder = new();
    private readonly List<(string Id, EmbeddingF32 Embedding)> _commands = [];

    /// <summary>
    /// Minimum cosine similarity required to consider a phrase a match.
    /// Lower = more lenient (more false positives), higher = stricter (more misses).
    /// Default: 0.50
    /// </summary>
    public float Threshold { get; set; } = 0.50f;

    /// <summary>
    /// The best match must score at least this much higher than the second-best match.
    /// Prevents random sentences from winning by being barely above threshold when
    /// multiple commands score similarly (ambiguous input).
    /// Default: 0.03
    /// </summary>
    public float MinConfidenceGap { get; set; } = 0.03f;

    internal void Register(string id, string description)
    {
        var embedding = _embedder.Embed(description);
        _commands.Add((id, embedding));
    }

    /// <summary>
    /// Resolves <paramref name="text"/> to a command ID, or <c>null</c> if no command
    /// exceeds the similarity threshold with a sufficient confidence gap over alternatives.
    /// </summary>
    /// <param name="text">The text to match. Ideally in the same language as the registered descriptions.</param>
    /// <param name="thresholdOverride">Override the instance <see cref="Threshold"/> for this call.</param>
    public string? Resolve(string text, float? thresholdOverride = null)
    {
        if (string.IsNullOrWhiteSpace(text) || _commands.Count == 0) return null;

        var input = _embedder.Embed(text);
        var minSimilarity = thresholdOverride ?? Threshold;

        string? bestId = null;
        float bestSimilarity = minSimilarity;
        float secondBestSimilarity = 0f;

        foreach (var (id, embedding) in _commands)
        {
            var similarity = LocalEmbedder.Similarity(input, embedding);
            if (similarity > bestSimilarity)
            {
                secondBestSimilarity = bestSimilarity;
                bestSimilarity = similarity;
                bestId = id;
            }
            else if (similarity > secondBestSimilarity)
            {
                secondBestSimilarity = similarity;
            }
        }

        if (bestId != null && bestSimilarity - secondBestSimilarity < MinConfidenceGap)
            return null;

        return bestId;
    }

    public void Dispose() => _embedder.Dispose();
}
