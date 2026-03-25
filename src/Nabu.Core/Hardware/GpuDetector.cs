using Microsoft.Win32;
using System.Runtime.Versioning;

namespace Nabu.Core.Hardware;

public static class GpuDetector
{
    public static GpuInfo Detect()
    {
        if (OperatingSystem.IsWindows()) return DetectWindows();
        if (OperatingSystem.IsLinux()) return DetectLinux();
        if (OperatingSystem.IsMacOS()) return new(true, "CoreML (Apple)");
        return new(false, "CPU");
    }

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