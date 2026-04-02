using Whisper.net.Ggml;
using Nabu.Core.Hardware;
using Nabu.Core.Models;

namespace Nabu.Core.ModelSetup;

/// <summary>
/// Orchestrates Whisper model selection, resolution, and download for the local Nabu server.
/// Determines whether to use a GPU float32 model or a CPU Q4_0-quantised model based on detected VRAM,
/// then downloads the selected model if it is not already present.
/// </summary>
public static class ModelManager
{
    private record ModelResolution(QuantizationType Quantization, string FileName, long ApproxBytes);

    /// <summary>
    /// Displays the interactive model selection menu in the console and returns the user's choice.
    /// </summary>
    /// <param name="modelsDirectory">Directory to check for already-downloaded model files.</param>
    /// <param name="gpuInfo">Detected GPU information used to derive recommendations and filter unavailable sizes.</param>
    /// <returns>
    /// A <see cref="ModelSelection"/> with the chosen size key and whether CPU mode was forced,
    /// or <c>null</c> if the user quit the menu.
    /// </returns>
    public static ModelSelection? PromptModelSize(string modelsDirectory, GpuInfo gpuInfo)
    {
        var recommended = ModelCatalog.GetRecommendedSize(gpuInfo.VramFreeMb);
        var unavailable = gpuInfo.IsGpu ? ModelCatalog.GetUnavailableSizes(gpuInfo.VramFreeMb) : null;
        var vramFreeMb = gpuInfo.IsGpu ? gpuInfo.VramFreeMb : null;
        var gpuLabel = gpuInfo.IsGpu ? gpuInfo.Label : null;
        var vramTotalMb = gpuInfo.IsGpu ? gpuInfo.VramTotalMb : null;
        var cpuName = GpuDetector.DetectCpuName();
        return ModelMenu.Prompt(ModelCatalog.MenuEntries, modelsDirectory, recommended, unavailable, vramFreeMb,
            vramTotalMb, gpuLabel, cpuName);
    }

    /// <summary>Detects the GPU on the current machine. Convenience wrapper around <see cref="GpuDetector.Detect"/>.</summary>
    public static GpuInfo DetectGpu()
        => GpuDetector.Detect();

    /// <summary>
    /// Ensures the required Whisper model file is present on disk, downloading it if necessary,
    /// and returns its file path together with display metadata.
    /// </summary>
    /// <param name="modelSize">Size key (one of <c>"tiny"</c>, <c>"base"</c>, <c>"small"</c>, <c>"medium"</c>, <c>"large"</c>).</param>
    /// <param name="modelsDirectory">Directory where model files are stored.</param>
    /// <param name="gpuInfo">GPU information used to decide between float32 and Q4_0 quantisation.</param>
    /// <param name="forceCpu">
    /// When <c>true</c>, always selects the Q4_0 quantised model regardless of available VRAM.
    /// </param>
    /// <returns>
    /// A tuple of the local model file path and a <see cref="LoadedModelInfo"/> with a display name and
    /// the inference mode label (e.g., <c>"CUDA (RTX 4090)"</c> or <c>"CPU"</c>).
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="modelSize"/> is not a recognised key.</exception>
    public static async Task<(string FilePath, LoadedModelInfo ModelInfo)> EnsureModelAsync(
        string modelSize, string modelsDirectory, GpuInfo gpuInfo, bool forceCpu = false)
    {
        if (!ModelCatalog.Models.TryGetValue(modelSize, out var modelInfo))
            throw new ArgumentException($"Unknown model size '{modelSize}'. Valid: tiny, base, small, medium, large.");

        var resolution = ResolveModel(modelInfo, gpuInfo, forceCpu);
        var filePath = Path.Combine(modelsDirectory, resolution.FileName);
        var usingGpu = resolution.Quantization == QuantizationType.NoQuantization;
        var mode = usingGpu
            ? gpuInfo.Label
            : gpuInfo.IsGpu && !forceCpu
                ? "CPU: Insufficient VRAM"
                : "CPU";

        if (!File.Exists(filePath))
        {
            PrintModelInfo(mode);
            await DownloadModel(modelInfo.GgmlType, resolution, filePath, modelsDirectory);
        }

        var quantLabel = usingGpu ? "no quantization" : "q4_0";
        var displayName = $"{char.ToUpper(modelSize[0])}{modelSize[1..]} ({quantLabel})";

        return (filePath, new LoadedModelInfo(displayName, mode));
    }

    private static ModelResolution ResolveModel(ModelInfo modelInfo, GpuInfo gpuInfo, bool forceCpu = false)
    {
        if (!gpuInfo.IsGpu || forceCpu)
            return new(QuantizationType.Q4_0, $"{modelInfo.BaseName}-q4_0.bin", modelInfo.Q4SizeBytes);

        var usableBytes = gpuInfo.VramFreeMb.HasValue
            ? (gpuInfo.VramFreeMb.Value - ModelCatalog.GetBufferMb(gpuInfo.VramFreeMb.Value)) * 1024 * 1024
            : long.MaxValue;

        return modelInfo.GpuSizeBytes <= usableBytes
            ? new(QuantizationType.NoQuantization, $"{modelInfo.BaseName}.bin", modelInfo.GpuSizeBytes)
            : new(QuantizationType.Q4_0, $"{modelInfo.BaseName}-q4_0.bin", modelInfo.Q4SizeBytes);
    }

    private static void PrintModelInfo(string mode)
    {
        Console.WriteLine($"{ModelMenu.Indent}Mode: {mode}");
        Console.WriteLine();
    }

    private static async Task DownloadModel(
        GgmlType ggmlType,
        ModelResolution resolution,
        string filePath,
        string modelsDirectory)
    {
        Directory.CreateDirectory(modelsDirectory);

        var label = $"{ModelMenu.Indent}Downloading {resolution.FileName}";
        Console.WriteLine(label + ".");
        int dotsRow = Console.CursorTop - 1;
        int dotsCol = label.Length;

        var consoleLock = new object();
        using var cts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            int tick = 0;
            while (!cts.IsCancellationRequested)
            {
                await Task.Delay(500);
                if (cts.IsCancellationRequested) break;
                tick++;
                var dots = new string('.', tick % 3 + 1).PadRight(3);
                try
                {
                    lock (consoleLock)
                    {
                        var (left, top) = Console.GetCursorPosition();
                        Console.SetCursorPosition(dotsCol, dotsRow);
                        Console.Write(dots);
                        Console.SetCursorPosition(left, top);
                    }
                }
                catch
                {
                }
            }
        });

        var tempFilePath = filePath + ".tmp";
        try
        {
            using var modelStream =
                await WhisperGgmlDownloader.Default.GetGgmlModelAsync(ggmlType, resolution.Quantization);

            using (var fileWriter = File.OpenWrite(tempFilePath))
                await ModelDownloader.DownloadAsync(modelStream, fileWriter, consoleLock, resolution.ApproxBytes);

            File.Move(tempFilePath, filePath);
        }
        catch
        {
            cts.Cancel();
            File.Delete(tempFilePath);
            throw;
        }

        cts.Cancel();
        Console.WriteLine();
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"{ModelMenu.Indent}Done.");
        Console.ResetColor();
        await Task.Delay(2500);
    }
}