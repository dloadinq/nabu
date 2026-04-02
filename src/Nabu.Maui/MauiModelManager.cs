using Nabu.Core.Hardware;
using Nabu.Core.Models;
using Whisper.net.Ggml;

namespace Nabu.Maui;

/// <summary>Represents one row in the model selection list.</summary>
public record MauiModelEntry(
    string Size,
    string Description,
    string GpuSize,
    string CpuSize,
    bool IsDownloaded,
    bool IsRecommended,
    bool RequiresForceCpu);

/// <summary>
/// MAUI-specific model management: GPU detection, model listing, and download with progress.
/// Models are stored in <see cref="ModelsDirectory"/> inside the app's data folder.
/// </summary>
public static class MauiModelManager
{
    public static string ModelsDirectory =>
        Path.Combine(FileSystem.AppDataDirectory, "models");

    public static GpuInfo DetectGpu() => GpuDetector.Detect();

    public static IReadOnlyList<MauiModelEntry> GetEntries(GpuInfo gpu)
    {
        var recommended = ModelCatalog.GetRecommendedSize(gpu.VramFreeMb);
        var unavailable = gpu.IsGpu ? ModelCatalog.GetUnavailableSizes(gpu.VramFreeMb) : [];

        return ModelCatalog.MenuEntries.Select(entry => new MauiModelEntry(
            entry.Size,
            entry.Label,
            entry.GpuSize,
            entry.Q4Size,
            IsOnDisk(entry.Size, gpu),
            entry.Size == recommended,
            unavailable.Contains(entry.Size)
        )).ToList();
    }

    /// <summary>Returns the resolved local file path for a model (may or may not exist yet).</summary>
    public static string GetModelPath(string size, GpuInfo gpu, bool forceCpu)
    {
        var info = ModelCatalog.Models[size];
        var fileName = UseQ4(gpu, forceCpu)
            ? $"{info.BaseName}-q4_0.bin"
            : $"{info.BaseName}.bin";
        return Path.Combine(ModelsDirectory, fileName);
    }

    /// <summary>
    /// Ensures the model file is on disk, downloading it if necessary.
    /// Reports progress in the range [0, 1]. Returns the local file path.
    /// </summary>
    public static async Task<string> EnsureModelAsync(
        string size,
        GpuInfo gpu,
        bool forceCpu,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var path = GetModelPath(size, gpu, forceCpu);
        if (File.Exists(path))
        {
            progress?.Report(1.0);
            return path;
        }

        var info = ModelCatalog.Models[size];
        var quantization = UseQ4(gpu, forceCpu) ? QuantizationType.Q4_0 : QuantizationType.NoQuantization;
        var approxBytes = UseQ4(gpu, forceCpu) ? info.Q4SizeBytes : info.GpuSizeBytes;

        Directory.CreateDirectory(ModelsDirectory);
        var tempPath = path + ".tmp";

        try
        {
            using var modelStream =
                await WhisperGgmlDownloader.Default.GetGgmlModelAsync(info.GgmlType, quantization);
            await using var fileStream = File.Create(tempPath);

            var buffer = new byte[81920];
            long downloaded = 0;
            int read;

            while ((read = await modelStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                downloaded += read;
                if (approxBytes > 0)
                    progress?.Report(Math.Min(0.99, (double)downloaded / approxBytes));
            }
        }
        catch
        {
            File.Delete(tempPath);
            throw;
        }

        File.Move(tempPath, path);
        progress?.Report(1.0);
        return path;
    }

    private static bool IsOnDisk(string size, GpuInfo gpu) =>
        File.Exists(GetModelPath(size, gpu, false)) ||
        File.Exists(GetModelPath(size, gpu, true));

    private static bool UseQ4(GpuInfo gpu, bool forceCpu) => !gpu.IsGpu || forceCpu;
}
