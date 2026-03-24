using Whisper.net.Ggml;

namespace Nabu.Core.Models;

internal record ModelInfo(GgmlType GgmlType, string BaseName, long GpuSizeBytes, long Q4SizeBytes);

internal static class ModelCatalog
{
    internal static readonly Dictionary<string, ModelInfo> Models =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["tiny"] = new(GgmlType.Tiny, "ggml-tiny", 79_000_000, 32_000_000),
            ["base"] = new(GgmlType.Base, "ggml-base", 148_000_000, 59_000_000),
            ["small"] = new(GgmlType.Small, "ggml-small", 488_000_000, 190_000_000),
            ["medium"] = new(GgmlType.Medium, "ggml-medium", 1_533_000_000, 514_000_000),
            ["large"] = new(GgmlType.LargeV3, "ggml-large-v3", 3_094_000_000, 1_080_000_000),
        };

    internal static readonly ModelMenuEntry[] MenuEntries =
    [
        Entry("tiny", "tiny\t- fastest, least accurate"),
        Entry("base", "base\t- fast, decent accuracy"),
        Entry("small", "small\t- balanced"),
        Entry("medium", "medium\t- good accuracy"),
        Entry("large", "large\t- best accuracy, slowest"),
    ];

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