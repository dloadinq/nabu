using Whisper.net.Ggml;
using Nabu.Core.Hardware;
using Nabu.Core.Models;

namespace Nabu.Core.ModelSetup;

public static class ModelManager
{
    private record ModelResolution(QuantizationType Quantization, string FileName, long ApproxBytes);

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

    public static GpuInfo DetectGpu()
        => GpuDetector.Detect();

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