using System.Management;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Nabu.Core.Hardware;

/// <summary>
/// Queries VRAM availability from multiple platform-specific sources (nvidia-smi, Windows WMI/Registry,
/// and Linux sysfs) to populate <see cref="VramInfo"/> for model selection.
/// </summary>
public static class VramMonitor
{
    /// <summary>
    /// Queries free and total VRAM for an NVIDIA GPU via <c>nvidia-smi</c>.
    /// Returns <c>null</c> values for either field if the query fails.
    /// </summary>
    public static VramInfo QueryNvidia()
    {
        var free = ParseLong(ProcessHelper.RunFirstLine("nvidia-smi",
            "--query-gpu=memory.free  --format=csv,noheader,nounits"));
        var total = ParseLong(ProcessHelper.RunFirstLine("nvidia-smi",
            "--query-gpu=memory.total --format=csv,noheader,nounits"));
        return new VramInfo(FreeMb: free, TotalMb: total);
    }

    /// <summary>
    /// Queries the GPU adapter name and VRAM from Windows WMI and the Windows Registry, filtering
    /// results to adapters whose names contain at least one of <paramref name="nameFilters"/>.
    /// </summary>
    /// <param name="nameFilters">Substrings used to match the adapter name (e.g., <c>"AMD"</c>, <c>"Intel"</c>).</param>
    /// <returns>The adapter display name and a <see cref="VramInfo"/> with free and total VRAM figures.</returns>
    [SupportedOSPlatform("windows")]
    public static (string? Name, VramInfo Vram) QueryWindowsAdapterInfo(params string[] nameFilters)
    {
        var (name, totalMb) = QueryAdapterNameAndTotal(nameFilters);
        var freeMb = QueryVramFreeMb(totalMb);
        return (name, new VramInfo(FreeMb: freeMb, TotalMb: totalMb));
    }

    /// <summary>
    /// Queries VRAM totals and usage from the Linux kernel's <c>/sys/class/drm</c> sysfs interface.
    /// Used for AMD and Intel GPUs on Linux where dedicated tooling may not be available.
    /// Returns <c>null</c> values when no compatible card is found or an I/O error occurs.
    /// </summary>
    public static VramInfo QuerySysfs()
    {
        try
        {
            foreach (var cardPath in Directory.EnumerateDirectories("/sys/class/drm")
                         .Where(p =>
                         {
                             var n = Path.GetFileName(p);
                             return n.StartsWith("card") && !n.Contains('-');
                         }))
            {
                var totalFile = Path.Combine(cardPath, "device", "mem_info_vram_total");
                var usedFile = Path.Combine(cardPath, "device", "mem_info_vram_used");
                if (!File.Exists(totalFile) || !File.Exists(usedFile)) continue;

                var totalStr = File.ReadAllText(totalFile).Trim();
                var usedStr = File.ReadAllText(usedFile).Trim();
                if (long.TryParse(totalStr, out var total) && long.TryParse(usedStr, out var used) && total > 0)
                    return new((total - used) / (1024 * 1024), total / (1024 * 1024));
            }
        }
        catch
        {
        }

        return new(null, null);
    }

    [SupportedOSPlatform("windows")]
    private static (string? Name, long? TotalMb) QueryAdapterNameAndTotal(string[] nameFilters)
    {
        try
        {
            var where = string.Join(" OR ", nameFilters.Select(f => $"Name LIKE '%{f}%'"));
            using var searcher = new ManagementObjectSearcher(
                $"SELECT Name, AdapterRAM FROM Win32_VideoController WHERE {where}");

            foreach (ManagementObject obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString();
                var totalMb = QueryTotalMbFromRegistry(nameFilters)
                              ?? AdapterRamToMb(obj["AdapterRAM"]);
                return (name, totalMb);
            }
        }
        catch
        {
        }

        return (null, null);
    }

    private static long? AdapterRamToMb(object? value)
    {
        if (value is not uint ram || ram == 0) return null;
        // WMI reports AdapterRAM as a uint, which wraps at 4 GB; values near the uint ceiling indicate overflow.
        if (ram >= uint.MaxValue - 1024 * 1024) return null;
        return ram / (1024 * 1024);
    }

    [SupportedOSPlatform("windows")]
    private static long? QueryTotalMbFromRegistry(string[] nameFilters)
    {
        try
        {
            const string gpuClass = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
            using var baseKey = Registry.LocalMachine.OpenSubKey(gpuClass);
            if (baseKey is null) return null;

            foreach (var subName in baseKey.GetSubKeyNames())
            {
                if (!int.TryParse(subName, out _)) continue;
                using var subKey = baseKey.OpenSubKey(subName);
                if (subKey is null) continue;

                var desc = subKey.GetValue("DriverDesc")?.ToString() ?? "";
                if (!nameFilters.Any(f => desc.Contains(f, StringComparison.OrdinalIgnoreCase))) continue;

                if (subKey.GetValue("HardwareInformation.qwMemorySize") is long bytes && bytes > 0)
                    return bytes / (1024 * 1024);
            }
        }
        catch
        {
        }

        return null;
    }

    [SupportedOSPlatform("windows")]
    private static long? QueryVramFreeMb(long? totalMb)
    {
        if (totalMb is null) return null;
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT DedicatedUsage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUAdapterMemory");

            long totalUsedBytes = 0;
            foreach (ManagementObject obj in searcher.Get())
                if (obj["DedicatedUsage"] is ulong used)
                    totalUsedBytes += (long)used;

            if (totalUsedBytes > 0)
                return Math.Max(0, totalMb.Value - totalUsedBytes / (1024 * 1024));
        }
        catch
        {
        }

        return totalMb;
    }

    private static long? ParseLong(string? s) => long.TryParse(s, out var v) ? v : null;
}