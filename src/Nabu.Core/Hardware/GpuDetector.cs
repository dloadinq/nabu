using Microsoft.Win32;
using System.Runtime.Versioning;

namespace Nabu.Core.Hardware;

/// <summary>
/// Detects the available GPU and VRAM on the current machine to inform model selection.
/// Supports Windows (CUDA, AMD Vulkan/ROCm, Intel), Linux (CUDA, AMD ROCm, Intel Vulkan), and macOS (CoreML).
/// Falls back to CPU when no supported GPU is found.
/// </summary>
public static class GpuDetector
{
    /// <summary>
    /// Detects the primary GPU and its available VRAM on the current operating system.
    /// </summary>
    /// <returns>
    /// A <see cref="GpuInfo"/> describing whether a GPU was found, its display label, and VRAM figures.
    /// When no GPU is detected, <see cref="GpuInfo.IsGpu"/> is <c>false</c> and the label is <c>"CPU"</c>.
    /// </returns>
    public static GpuInfo Detect()
    {
        if (OperatingSystem.IsWindows()) return DetectWindows();
        if (OperatingSystem.IsLinux()) return DetectLinux();
        if (OperatingSystem.IsMacOS()) return new(true, "CoreML (Apple)");
        return new(false, "CPU");
    }

    /// <summary>
    /// Attempts to read the CPU model name from OS-specific sources
    /// (Windows Registry, <c>/proc/cpuinfo</c> on Linux, or <c>sysctl</c> on macOS).
    /// </summary>
    /// <returns>The trimmed CPU name string, or <c>null</c> if it cannot be determined.</returns>
    public static string? DetectCpuName()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var name = Registry.GetValue(
                    @"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                    "ProcessorNameString", null) as string;
                return name?.Trim();
            }
            catch
            {
            }

            return null;
        }

        if (OperatingSystem.IsLinux())
        {
            try
            {
                foreach (var line in File.ReadLines("/proc/cpuinfo"))
                {
                    if (!line.StartsWith("model name", StringComparison.OrdinalIgnoreCase)) continue;
                    var colon = line.IndexOf(':');
                    if (colon >= 0) return line[(colon + 1)..].Trim();
                }
            }
            catch
            {
            }

            return null;
        }

        if (OperatingSystem.IsMacOS())
            return ProcessHelper.RunFirstLine("sysctl", "-n machdep.cpu.brand_string");

        return null;
    }

    [SupportedOSPlatform("windows")]
    private static GpuInfo DetectWindows()
    {
        var sys = Environment.GetFolderPath(Environment.SpecialFolder.System);

        if (File.Exists(Path.Combine(sys, "nvcuda.dll")))
        {
            var name = ProcessHelper.RunFirstLine("nvidia-smi", "--query-gpu=name --format=csv,noheader,nounits");
            var vram = VramMonitor.QueryNvidia();
            return new GpuInfo(
                IsGpu: true,
                Label: name is not null ? $"CUDA ({name})" : "CUDA (NVIDIA)",
                VramFreeMb: vram.FreeMb,
                VramTotalMb: vram.TotalMb);
        }

        if (File.Exists(Path.Combine(sys, "amdvlk64.dll")) ||
            File.Exists(Path.Combine(sys, "amdxc64.dll")) ||
            File.Exists(Path.Combine(sys, "atiuxpag.dll")))
        {
            var (name, vram) = VramMonitor.QueryWindowsAdapterInfo("AMD", "Radeon", "ATI");
            return new(true, name is not null ? $"Vulkan ({name})" : "Vulkan (AMD)", vram.FreeMb, vram.TotalMb);
        }

        if (File.Exists(Path.Combine(sys, "igd12umd64.dll")) ||
            File.Exists(Path.Combine(sys, "igdogl64.dll")) ||
            File.Exists(Path.Combine(sys, "igdlh64.dll")))
        {
            var (name, vram) = VramMonitor.QueryWindowsAdapterInfo("Intel");
            return new(true, name is not null ? $"Vulkan / OpenVINO ({name})" : "Vulkan / OpenVINO (Intel)",
                vram.FreeMb, vram.TotalMb);
        }

        if (File.Exists(Path.Combine(sys, "vulkan-1.dll")))
            return new(true, "Vulkan");

        return new(false, "CPU");
    }

    private static GpuInfo DetectLinux()
    {
        const string debian = "/usr/lib/x86_64-linux-gnu";
        const string rpm = "/usr/lib64";

        if (File.Exists($"{debian}/libcuda.so.1") || File.Exists($"{rpm}/libcuda.so.1"))
        {
            var name = ProcessHelper.RunFirstLine("nvidia-smi", "--query-gpu=name --format=csv,noheader,nounits");
            var vram = VramMonitor.QueryNvidia();
            return new(true, name is not null ? $"CUDA ({name})" : "CUDA (NVIDIA)", vram.FreeMb, vram.TotalMb);
        }

        if (File.Exists($"{debian}/libvulkan_radeon.so") || File.Exists($"{debian}/libdrm_amdgpu.so.1"))
        {
            var name = ProcessHelper.RunFirstLine("rocm-smi", "--showproductname");
            var vram = VramMonitor.QuerySysfs();
            return new(true, name is not null ? $"Vulkan ({name})" : "Vulkan (AMD)", vram.FreeMb, vram.TotalMb);
        }

        if (File.Exists($"{debian}/libvulkan_intel.so"))
        {
            var vram = VramMonitor.QuerySysfs();
            return new(true, "Vulkan / OpenVINO (Intel)", vram.FreeMb, vram.TotalMb);
        }

        return new(false, "CPU");
    }
}