using Whisper.net.Ggml;

namespace Nabu.Core.Models;

public record ModelInfo(GgmlType GgmlType, string BaseName, long GpuSizeBytes, long Q4SizeBytes);

public static class ModelCatalog
{
    public static readonly Dictionary<string, ModelInfo> Models =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["tiny"] = new(GgmlType.Tiny, "ggml-tiny", 79_000_000, 32_000_000),
            ["base"] = new(GgmlType.Base, "ggml-base", 148_000_000, 59_000_000),
            ["small"] = new(GgmlType.Small, "ggml-small", 488_000_000, 190_000_000),
            ["medium"] = new(GgmlType.Medium, "ggml-medium", 1_533_000_000, 514_000_000),
            ["large"] = new(GgmlType.LargeV3, "ggml-large-v3", 3_094_000_000, 1_080_000_000),
        };

    public static readonly ModelMenuEntry[] MenuEntries =
    [
        Entry("tiny", "tiny\t- fastest, least accurate"),
        Entry("base", "base\t- fast, decent accuracy"),
        Entry("small", "small\t- balanced"),
        Entry("medium", "medium\t- good accuracy"),
        Entry("large", "large\t- best accuracy, slowest"),
    ];

    private const double VramBufferFactor = 0.10;
    private const long VramBufferMinMb = 100;

    public static long GetBufferMb(long vramFreeMb) =>
        Math.Max((long)(vramFreeMb * VramBufferFactor), VramBufferMinMb);

    public static string? GetRecommendedSize(long? vramFreeMb)
    {
        if (vramFreeMb is null) return null;
        var usableBytes = (vramFreeMb.Value - GetBufferMb(vramFreeMb.Value)) * 1024 * 1024;
        return Models
            .OrderByDescending(kv => kv.Value.GpuSizeBytes)
            .FirstOrDefault(kv => kv.Value.GpuSizeBytes <= usableBytes)
            .Key;
    }

    public static HashSet<string> GetUnavailableSizes(long? vramFreeMb)
    {
        if (vramFreeMb is null) return [];
        var usableBytes = (vramFreeMb.Value - GetBufferMb(vramFreeMb.Value)) * 1024 * 1024;
        return Models
            .Where(kv => kv.Value.GpuSizeBytes > usableBytes)
            .Select(kv => kv.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static ModelMenuEntry Entry(string size, string label)
    {
        var info = Models[size];
        return new ModelMenuEntry(
            size,
            label,
            info.BaseName,
            FormatApproxSize(info.GpuSizeBytes),
            FormatApproxSize(info.Q4SizeBytes));
    }

    private static string FormatApproxSize(long bytes)
    {
        const long gigaByte = 1_000_000_000;
        const long megaByte = 1_000_000;

        if (bytes >= gigaByte)
            return $"~{bytes / (double)gigaByte:F1} GB";

        long mb = bytes / megaByte;
        int roundTo = mb >= 100 ? 10 : 5;
        long rounded = (long)(Math.Round((double)mb / roundTo) * roundTo);
        return $"~{rounded} MB";
    }
}