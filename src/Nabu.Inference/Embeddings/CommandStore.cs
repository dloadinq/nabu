using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Nabu.Inference.Embeddings;

/// <summary>
/// File-backed JSON store for per-origin command embedding collections.
/// Each origin (Blazor app) gets its own JSON file identified by a sanitised hostname+port slug.
/// All read/write operations are serialised by an internal lock.
/// </summary>
public sealed class CommandStore
{
    private readonly string _storeDir;
    private readonly Lock _lock = new();

    /// <summary>
    /// Initialises the store and ensures the storage directory exists.
    /// </summary>
    /// <param name="storeDir">Directory path where collection JSON files are persisted.</param>
    public CommandStore(string storeDir)
    {
        _storeDir = storeDir;
        Directory.CreateDirectory(storeDir);
    }

    /// <summary>
    /// Derives a filesystem-safe collection name from an origin URL
    /// (e.g., <c>"https://myapp.com"</c> becomes <c>"_commands_myapp_com_443"</c>).
    /// Returns <c>"_commands_unknown"</c> when the URI cannot be parsed.
    /// </summary>
    /// <param name="origin">The HTTP origin of the Blazor application.</param>
    public static string CollectionName(string origin)
    {
        try
        {
            var uri = new Uri(origin);
            var safe = $"{uri.Host}_{uri.Port}"
                .Replace(".", "_")
                .Replace("-", "_");
            return $"_commands_{safe}";
        }
        catch
        {
            return "_commands_unknown";
        }
    }

    private string FilePath(string collectionName)
    {
        return Path.Combine(_storeDir, $"{collectionName}.json");
    }

    private List<CommandEntry> Load(string collectionName)
    {
        var path = FilePath(collectionName);
        if (!File.Exists(path)) return [];
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize(json, NabuInferenceJsonContext.Default.CommandEntryArray)
                ?.ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }

    private void Save(string collectionName, List<CommandEntry> entries)
    {
        var json = JsonSerializer.Serialize(entries.ToArray(), NabuInferenceJsonContext.Default.CommandEntryArray);
        File.WriteAllText(FilePath(collectionName), json);
    }

    /// <summary>
    /// Returns the content hashes of all entries currently persisted in the named collection.
    /// The Blazor client uses this list to compute a delta and send only changed phrases.
    /// </summary>
    /// <param name="collectionName">The collection identifier (see <see cref="CollectionName"/>).</param>
    public string[] GetHashes(string collectionName)
    {
        lock (_lock)
        {
            var entries = Load(collectionName);
            var result = new string[entries.Count];
            for (var i = 0; i < entries.Count; i++) result[i] = entries[i].Hash;
            return result;
        }
    }

    /// <summary>
    /// Applies a delta patch to the named collection: removes entries whose hashes are not in
    /// <paramref name="retainedHashes"/>, then upserts each item in <paramref name="upsertItems"/>
    /// by computing or updating its embedding vector via <paramref name="embedder"/>.
    /// Saves the collection to disk only when at least one change was made.
    /// </summary>
    /// <param name="collectionName">Target collection identifier.</param>
    /// <param name="upsertItems">New or changed command phrases to embed and store.</param>
    /// <param name="retainedHashes">
    /// Hashes of all phrases the client still considers valid. Entries absent from this set are deleted.
    /// </param>
    /// <param name="embedder">The sentence embedder used to compute vectors for upserted phrases.</param>
    /// <param name="logger">Logger for recording insert/update/removal counts.</param>
    public void Patch(string collectionName, CommandSyncItem[] upsertItems, string[] retainedHashes,
        SentenceEmbedder embedder, ILogger logger)
    {
        lock (_lock)
        {
            var entries = Load(collectionName);
            var retainedSet = new HashSet<string>(retainedHashes, StringComparer.Ordinal);

            var removed = entries.RemoveAll(e => !retainedSet.Contains(e.Hash));
            if (removed > 0) logger.LogInformation("Removed {Count} stale embeddings based on delta patch", removed);

            foreach (var item in upsertItems)
            {
                var existing = entries.FirstOrDefault(e => e.CommandId == item.CommandId && e.Text == item.Text);
                if (existing != null && existing.Hash == item.Hash) continue;

                var vector = embedder.Embed(item.Text);
                if (existing != null)
                {
                    existing.Hash = item.Hash;
                    existing.Scope = item.Scope;
                    existing.ExcludeScope = item.ExcludeScope;
                    existing.Vector = vector;
                    logger.LogInformation("Updated embedding [{Id}]: \"{Text}\"", item.CommandId, item.Text);
                }
                else
                {
                    entries.Add(new CommandEntry
                    {
                        CommandId = item.CommandId, Text = item.Text, Hash = item.Hash, Scope = item.Scope,
                        ExcludeScope = item.ExcludeScope, Vector = vector
                    });
                    logger.LogInformation("Inserted embedding [{Id}]: \"{Text}\"", item.CommandId, item.Text);
                }
            }

            if (removed > 0 || upsertItems.Length > 0) Save(collectionName, entries);
        }
    }

    /// <summary>
    /// Finds the best-matching command for a query embedding vector using cosine similarity,
    /// applying scope filtering and confidence thresholds.
    /// </summary>
    /// <param name="collectionName">Collection to search.</param>
    /// <param name="queryVector">384-dimensional embedding of the spoken utterance.</param>
    /// <param name="threshold">
    /// Minimum cosine similarity score required to consider any match valid. Queries that score
    /// below this value return <c>null</c>.
    /// </param>
    /// <param name="minConfidenceGap">
    /// Minimum difference between the top-1 and top-2 scores required when the top score is below
    /// <paramref name="highConfidenceThreshold"/>. Prevents ambiguous matches.
    /// </param>
    /// <param name="highConfidenceThreshold">
    /// When the top score meets or exceeds this value, the gap check is skipped and the match is
    /// returned immediately.
    /// </param>
    /// <param name="logger">Optional logger for diagnostic score output.</param>
    /// <param name="currentRoute">
    /// The current application route, used to enforce scope and exclude-scope filtering.
    /// </param>
    /// <returns>The matching command identifier, or <c>null</c> if no match passes all thresholds.</returns>
    public string? Resolve(
        string collectionName,
        float[] queryVector,
        float threshold = 0.50f,
        float minConfidenceGap = 0.03f,
        float highConfidenceThreshold = 0.85f,
        ILogger? logger = null,
        string? currentRoute = null)
    {
        List<CommandEntry> entries;
        lock (_lock)
        {
            entries = Load(collectionName);
        }

        var bestPerCommand = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry.Scope != null && currentRoute != null &&
                !currentRoute.StartsWith(entry.Scope, StringComparison.OrdinalIgnoreCase)) continue;
            if (entry.ExcludeScope != null && currentRoute != null &&
                currentRoute.Equals(entry.ExcludeScope, StringComparison.OrdinalIgnoreCase)) continue;

            var score = SentenceEmbedder.CosineSimilarity(queryVector, entry.Vector);
            if (!bestPerCommand.TryGetValue(entry.CommandId, out var existing) || score > existing) bestPerCommand[entry.CommandId] = score;
        }

        if (bestPerCommand.Count == 0) return null;

        var ranked = bestPerCommand.OrderByDescending(kv => kv.Value).Take(3).ToList();
        var top1 = ranked[0];
        var top2 = ranked.Count > 1 ? ranked[1] : default;
        var top3 = ranked.Count > 2 ? ranked[2] : default;
        var gap = top1.Value - top2.Value;

        logger?.LogInformation(
            "[Embeddings] #1: {S1:F3} ({Id1}), #2: {S2:F3} ({Id2}), #3: {S3:F3} ({Id3}), gap: {Gap:F3}",
            top1.Value, top1.Key, top2.Value, top2.Key, top3.Value, top3.Key, gap);

        if (top1.Value < threshold) return null;
        if (top1.Value >= highConfidenceThreshold) return top1.Key;
        if (gap < minConfidenceGap) return null;
        return top1.Key;
    }
}