using Whisper.net.Ggml;

namespace Nabu.Core.Models;

public static class ModelManager
{
    private record ModelResolution(QuantizationType Quantization, string FileName, long ApproxBytes);

    public static string PromptModelSize(string modelsDirectory)
        => ModelMenu.Prompt(ModelCatalog.MenuEntries, modelsDirectory);

    public static GpuInfo DetectGpu()
        => GpuDetector.Detect();

    public static async Task<string> EnsureModelAsync(string modelSize, string modelsDirectory)
    {
        if (!ModelCatalog.Models.TryGetValue(modelSize, out var modelInfo))
            throw new ArgumentException($"Unknown model size '{modelSize}'. Valid: tiny, base, small, medium, large.");

        var gpuInfo = GpuDetector.Detect();
        var resolution = ResolveModel(modelInfo, gpuInfo.IsGpu);
        var filePath = Path.Combine(modelsDirectory, resolution.FileName);

        PrintModelInfo(gpuInfo, filePath);

        if (!File.Exists(filePath))
            await DownloadModel(modelInfo.GgmlType, resolution, filePath, modelsDirectory);

        return filePath;
    }

    private static ModelResolution ResolveModel(ModelInfo modelInfo, bool isGpu) => isGpu
        ? new(QuantizationType.NoQuantization, $"{modelInfo.BaseName}.bin", modelInfo.GpuSizeBytes)
        : new(QuantizationType.Q4_0, $"{modelInfo.BaseName}-q4_0.bin", modelInfo.Q4SizeBytes);

    private static void PrintModelInfo(GpuInfo gpuInfo, string filePath)
    {
        Console.WriteLine(gpuInfo.IsGpu
            ? $"GPU: {gpuInfo.Label}"
            : "GPU: none — using Q4_0 quantized model");

        if (File.Exists(filePath))
            Console.WriteLine($"Model: {filePath}");
    }

    private static async Task DownloadModel(
        GgmlType ggmlType,
        ModelResolution resolution,
        string filePath,
        string modelsDirectory)
    {
        Directory.CreateDirectory(modelsDirectory);
        Console.WriteLine($"Downloading {resolution.FileName}...");

        using var modelStream =
            await WhisperGgmlDownloader.Default.GetGgmlModelAsync(ggmlType, resolution.Quantization);
        using var fileWriter = File.OpenWrite(filePath);

        await ModelDownloader.DownloadAsync(modelStream, fileWriter, resolution.ApproxBytes);
        Console.WriteLine();
    }
}