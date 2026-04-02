using System.Diagnostics;

namespace Nabu.Inference.Embeddings;

/// <summary>
/// Downloads the <c>all-MiniLM-L6-v2</c> ONNX model and its vocabulary file from Hugging Face
/// into a local directory if they are not already present.
/// Displays a console progress bar during the download.
/// </summary>
public static class EmbeddingModelSetup
{
    private const string BaseUrl = "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main";
    private const string ModelUrl = $"{BaseUrl}/onnx/model.onnx";
    private const string VocabUrl = $"{BaseUrl}/vocab.txt";

    public const string ModelFileName = "model.onnx";
    public const string VocabFileName = "vocab.txt";

    /// <summary>
    /// Ensures both <c>model.onnx</c> and <c>vocab.txt</c> are present in <paramref name="directory"/>.
    /// If either file is missing, it is downloaded from Hugging Face with progress output to the console.
    /// </summary>
    /// <param name="directory">Local directory where the embedding model files should be stored.</param>
    /// <returns><c>true</c> when the files are available (either pre-existing or just downloaded).</returns>
    public static async Task<bool> EnsureDownloadedAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var modelPath = Path.Combine(directory, ModelFileName);
        var vocabPath = Path.Combine(directory, VocabFileName);

        if (File.Exists(modelPath) && File.Exists(vocabPath))
            return true;

        Console.WriteLine();
        Console.WriteLine("English embedding model not found. Downloading...");
        Console.WriteLine("(all-MiniLM-L6-v2, ~90 MB)");
        Console.WriteLine();

        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromMinutes(10);

        var consoleLock = new Lock();

        if (!File.Exists(vocabPath))
        {
            Console.WriteLine("Downloading vocab.txt...");
            await using var vocabResponseStream = await httpClient.GetStreamAsync(VocabUrl);
            await using var vocabFileStream = File.Create(vocabPath);
            await DownloadAsync(vocabResponseStream, vocabFileStream, consoleLock, expectedTotalBytes: 0);
            Console.WriteLine();
        }

        if (!File.Exists(modelPath))
        {
            Console.WriteLine($"Downloading {ModelFileName}...");
            await using var modelResponseStream = await httpClient.GetStreamAsync(ModelUrl);
            await using var modelFileStream = File.Create(modelPath);
            await DownloadAsync(modelResponseStream, modelFileStream, consoleLock, expectedTotalBytes: 90_000_000);
            Console.WriteLine();
        }

        return true;
    }

    private static async Task DownloadAsync(Stream responseStream, Stream fileStream, Lock consoleLock,
        long expectedTotalBytes)
    {
        var buffer = new byte[81920];
        long totalBytesDownloaded = 0;
        int bytesRead;
        var slidingSpeedWindow = new Queue<(long ElapsedMs, long BytesDownloaded)>();
        var stopwatch = Stopwatch.StartNew();
        double currentSpeedBytesPerSecond = 0;

        while ((bytesRead = await responseStream.ReadAsync(buffer)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
            totalBytesDownloaded += bytesRead;

            var elapsedMs = stopwatch.ElapsedMilliseconds;
            slidingSpeedWindow.Enqueue((elapsedMs, totalBytesDownloaded));
            while (slidingSpeedWindow.Count > 1 && elapsedMs - slidingSpeedWindow.Peek().ElapsedMs > 5_000)
            {
                slidingSpeedWindow.Dequeue();
            }
            
            if (slidingSpeedWindow.Count >= 2)
            {
                var windowStart = slidingSpeedWindow.Peek();
                var windowDurationMs = elapsedMs - windowStart.ElapsedMs;
                currentSpeedBytesPerSecond = windowDurationMs > 0
                    ? (totalBytesDownloaded - windowStart.BytesDownloaded) / (double)windowDurationMs * 1000.0
                    : 0;
            }

            lock (consoleLock)
            {
                Console.Write(BuildProgressLine(totalBytesDownloaded, expectedTotalBytes, currentSpeedBytesPerSecond));
            }
        }

        lock (consoleLock)
        {
            Console.Write(BuildProgressLine(totalBytesDownloaded, totalBytesDownloaded, currentSpeedBytesPerSecond));
        }
    }

    private static string BuildProgressLine(long bytesDownloaded, long totalBytes, double speedBytesPerSecond)
    {
        const int barWidth = 28;
        var filledWidth = totalBytes > 0 ? (int)Math.Min(barWidth, bytesDownloaded * barWidth / totalBytes) : 0;
        var progressBar = new string('=', filledWidth).PadRight(barWidth).ToCharArray();
        if (totalBytes > 0)
        {
            var percentageLabel = $"{bytesDownloaded * 100 / totalBytes}%";
            var labelStartPosition = (barWidth - percentageLabel.Length) / 2;
            for (var charIndex = 0; charIndex < percentageLabel.Length; charIndex++)
            {
                progressBar[labelStartPosition + charIndex] = percentageLabel[charIndex];
            }        
        }

        var sizeLabel = totalBytes > 0
            ? $"{bytesDownloaded / 1_048_576.0:F0} MB / {totalBytes / 1_048_576} MB"
            : $"{bytesDownloaded / 1_048_576.0:F0} MB";

        var speedLabel = speedBytesPerSecond switch
        {
            >= 1_048_576 => $"{speedBytesPerSecond / 1_048_576:F1} MB/s",
            >= 1_024 => $"{speedBytesPerSecond / 1_024:F0} KB/s",
            _ => "--"
        };

        return $"\r[{new string(progressBar)}]\t{sizeLabel}  {speedLabel}   ";
    }
}