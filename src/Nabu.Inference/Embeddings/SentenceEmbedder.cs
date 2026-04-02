using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace Nabu.Inference.Embeddings;

/// <summary>
/// Produces L2-normalised 384-dimensional sentence embeddings using the <c>all-MiniLM-L6-v2</c> ONNX model
/// with WordPiece (BERT) tokenisation.
/// Optimised for English semantic similarity tasks; used to match spoken commands against registered phrases.
/// </summary>
/// <remarks>
/// The underlying <see cref="Microsoft.ML.OnnxRuntime.InferenceSession"/> is thread-safe and shared
/// across concurrent <see cref="Embed"/> calls.
/// </remarks>
public sealed class SentenceEmbedder : IDisposable
{
    private const int Dimensions = 384;
    private const int MaxSequenceLength = 128;

    private readonly InferenceSession _session;
    private readonly BertTokenizer _tokenizer;

    /// <summary>
    /// Loads the ONNX model and vocabulary from disk and configures the ONNX Runtime session with all
    /// graph optimisations enabled.
    /// </summary>
    /// <param name="modelPath">Path to the <c>model.onnx</c> file.</param>
    /// <param name="vocabPath">Path to the <c>vocab.txt</c> WordPiece vocabulary file.</param>
    public SentenceEmbedder(string modelPath, string vocabPath)
    {
        var sessionOpts = new SessionOptions();
        sessionOpts.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
        _session = new InferenceSession(modelPath, sessionOpts);
        _tokenizer = BertTokenizer.Create(vocabPath);
    }

    /// <summary>
    /// Tokenises <paramref name="text"/>, runs it through the transformer model, applies mean pooling
    /// over the last hidden state, and returns the L2-normalised result.
    /// The output is a 384-dimensional unit vector suitable for cosine similarity comparison.
    /// </summary>
    /// <param name="text">The English-language input sentence to embed.</param>
    /// <returns>A normalised float array of length 384.</returns>
    public float[] Embed(string text)
    {
        var tokenIds = _tokenizer.EncodeToIds(text, MaxSequenceLength, out _, out _);
        var sequenceLength = tokenIds.Count;

        var inputIds = new long[sequenceLength];
        var attentionMask = new long[sequenceLength];
        var tokenTypeIds = new long[sequenceLength];

        for (var tokenIndex = 0; tokenIndex < sequenceLength; tokenIndex++)
        {
            inputIds[tokenIndex] = tokenIds[tokenIndex];
            attentionMask[tokenIndex] = 1L;
        }

        int[] batchShape = [1, sequenceLength];
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids",
                new DenseTensor<long>(inputIds, batchShape)),
            NamedOnnxValue.CreateFromTensor("attention_mask",
                new DenseTensor<long>(attentionMask, batchShape)),
            NamedOnnxValue.CreateFromTensor("token_type_ids",
                new DenseTensor<long>(tokenTypeIds, batchShape))
        };

        using var outputs = _session.Run(inputs);
        var hiddenStates = outputs.First(o => o.Name == "last_hidden_state")
            .AsEnumerable<float>().ToArray();

        var meanPooled = new float[Dimensions];
        for (var tokenIndex = 0; tokenIndex < sequenceLength; tokenIndex++)
        for (var dimension = 0; dimension < Dimensions; dimension++)
        {
            meanPooled[dimension] += hiddenStates[tokenIndex * Dimensions + dimension];
        }
        for (var dimension = 0; dimension < Dimensions; dimension++)
        {
            meanPooled[dimension] /= sequenceLength;
        }
        
        var squaredNorm = 0f;
        foreach (var value in meanPooled)
        {
            squaredNorm += value * value;
        }
        var l2Norm = MathF.Sqrt(squaredNorm);
        
        if (!(l2Norm > 1e-9f)) return meanPooled;
        for (var dimension = 0; dimension < Dimensions; dimension++)
        {
            meanPooled[dimension] /= l2Norm;
        }
        return meanPooled;
    }

    /// <summary>
    /// Computes the cosine similarity between two L2-normalised vectors as a simple dot product.
    /// Both vectors must have the same length (384 for this model).
    /// </summary>
    /// <param name="a">First normalised embedding vector.</param>
    /// <param name="b">Second normalised embedding vector.</param>
    /// <returns>Cosine similarity in the range [−1, 1]; higher values indicate greater semantic similarity.</returns>
    public static float CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        var dot = 0f;
        for (var i = 0; i < a.Length; i++) dot += a[i] * b[i];
        return dot;
    }

    public void Dispose()
    {
        _session.Dispose();
    }
}