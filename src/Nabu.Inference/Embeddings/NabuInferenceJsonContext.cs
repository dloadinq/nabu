using System.Text.Json.Serialization;

namespace Nabu.Inference.Embeddings;

/// <summary>
/// Source-generated <see cref="System.Text.Json.Serialization.JsonSerializerContext"/> for the types
/// used in the embedding command store, enabling AOT-compatible, reflection-free JSON serialisation.
/// </summary>
[JsonSerializable(typeof(CommandEntry))]
[JsonSerializable(typeof(CommandEntry[]))]
[JsonSerializable(typeof(CommandSyncItem))]
[JsonSerializable(typeof(CommandSyncItem[]))]
public partial class NabuInferenceJsonContext : JsonSerializerContext
{
}
