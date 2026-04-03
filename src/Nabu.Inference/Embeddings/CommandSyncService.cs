using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nabu.Inference.Embeddings;

/// <summary>
/// Singleton service orchestrating command embedding sync and voice command resolution.
/// Receives delta-sync requests from Blazor clients, delegates persistence to <see cref="CommandStore"/>,
/// and resolves transcribed text to command identifiers via cosine similarity on sentence embeddings.
/// </summary>
/// <remarks>
/// Thread-safe: the file-backed JSON store and the OnnxRuntime <c>InferenceSession</c> are both
/// safe for concurrent access.
/// </remarks>
public sealed class CommandSyncService(
    CommandStore store,
    SentenceEmbedder embedder,
    IOptions<EmbeddingOptions> options,
    ILogger<CommandSyncService> logger)
    : IDisposable
{
    private readonly EmbeddingOptions _options = options.Value;

    /// <summary>
    /// Derives a filesystem-safe collection name from the origin URL of a Blazor client.
    /// Delegates to <see cref="CommandStore.CollectionName"/>.
    /// </summary>
    /// <param name="origin">The HTTP origin of the Blazor application.</param>
    public static string CollectionName(string origin)
    {
        return CommandStore.CollectionName(origin);
    }

    /// <summary>
    /// Returns the content hashes of all phrases currently stored for the given collection.
    /// The client uses this to compute a minimal delta before calling <see cref="PatchAsync"/>.
    /// </summary>
    /// <param name="collectionName">Target collection identifier.</param>
    public string[] GetHashes(string collectionName)
    {
        return store.GetHashes(collectionName);
    }

    /// <summary>
    /// Applies a delta patch to the named collection on a background thread.
    /// New or changed phrases are embedded and persisted; stale entries are removed.
    /// </summary>
    /// <param name="collectionName">Target collection identifier.</param>
    /// <param name="upserts">Phrases to insert or update.</param>
    /// <param name="retainedHashes">Hashes of all still-valid phrases; entries not in this list are removed.</param>
    public Task PatchAsync(string collectionName, CommandSyncItem[] upserts, string[] retainedHashes)
    {
        return Task.Run(() =>
        {
            logger.LogInformation(
                "[Embeddings] Delta patch: {Upserts} upserts, {Retained} retained hashes for {Collection}",
                upserts.Length, retainedHashes.Length, collectionName);
            store.Patch(collectionName, upserts, retainedHashes, embedder, logger);
            logger.LogInformation("[Embeddings] Patch complete for {Collection}", collectionName);
        });
    }

    private const string Fillers =
        "please|hey|hi|OK|ok|okay|" +
        "jarvis|alexa|siri|cortana|computer|assistant|bot|buddy|friend|" +
        "i'm|i'll|i've|i'd|i|me|you|we|they|he|she|it";

    private static readonly Regex FillerRegex = new(
        $@"^\s*\b({Fillers})\b[.,!?]?\s*|"
        + $@"\s*[,.]?\s*\b({Fillers})\b[.,!?]?\s*$|"
        + $@"\s+\b({Fillers})\b\s+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static string StripFillers(string text)
    {
        string result;
        do
        {
            result = FillerRegex.Replace(text, " ").Trim();
            if (result == text) break;
            text = result;
        } while (true);

        return result;
    }

    /// <summary>
    /// Strips common filler words from <paramref name="text"/>, embeds the result, and returns the
    /// best-matching command identifier from the named collection.
    /// </summary>
    /// <param name="collectionName">Collection to search.</param>
    /// <param name="text">Raw transcription text from Whisper (in English).</param>
    /// <param name="currentRoute">
    /// Current application route used for scope filtering, or <c>null</c> to ignore scopes.
    /// </param>
    /// <returns>The matched command identifier, or <c>null</c> when no confident match is found.</returns>
    public string? Resolve(string collectionName, string text, string? currentRoute = null)
    {
        var normalized = StripFillers(text);
        if (normalized != text) logger.LogDebug("Text normalized: '{Original}' -> '{Normalized}'", text, normalized);

        var vector = embedder.Embed(normalized);
        var result = store.Resolve(
            collectionName, 
            vector,
            _options.Threshold, 
            _options.MinConfidenceGap,
            _options.HighConfidenceThreshold, 
            logger,
            currentRoute);

        if (result != null)
            logger.LogInformation("Resolved: '{Normalized}' => '{Result}'", normalized, result);
        else
            logger.LogWarning("No match found for: '{Normalized}'", normalized);

        return result;
    }

    public void Dispose()
    {
        embedder.Dispose();
    }
}