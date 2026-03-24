using System.Diagnostics;

namespace Nabu.Core.Models;

internal static class GpuDetector
{
    public static GpuInfo Detect()
    {
        if (OperatingSystem.IsWindows()) return DetectWindows();
        if (OperatingSystem.IsLinux()) return DetectLinux();
        if (OperatingSystem.IsMacOS()) return new(true, "CoreML (Apple)");
        return new(false, "CPU");
    }

    private static GpuInfo DetectWindows()
    {
        var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);

        if (File.Exists(Path.Combine(systemDirectory, "nvcuda.dll")))
        {
            var gpuName = TryRunCli("nvidia-smi", "--query-gpu=name --format=csv,noheader,nounits");
            return new(true, gpuName is not null ? $"CUDA ({gpuName})" : "CUDA (NVIDIA)");
        }

        if (File.Exists(Path.Combine(systemDirectory, "amdvlk64.dll")) ||
            File.Exists(Path.Combine(systemDirectory, "amdxc64.dll")) ||
            File.Exists(Path.Combine(systemDirectory, "atiuxpag.dll")))
            return new(true, "Vulkan (AMD)");

        if (File.Exists(Path.Combine(systemDirectory, "igd12umd64.dll")) ||
            File.Exists(Path.Combine(systemDirectory, "igdogl64.dll")) ||
            File.Exists(Path.Combine(systemDirectory, "igdlh64.dll")))
            return new(true, "Vulkan / OpenVINO (Intel)");

        if (File.Exists(Path.Combine(systemDirectory, "vulkan-1.dll")))
            return new(true, "Vulkan");

        return new(false, "CPU");
    }

    private static GpuInfo DetectLinux()
    {
        const string debianLibDirectory = "/usr/lib/x86_64-linux-gnu";
        const string rpmLibDirectory = "/usr/lib64";

        if (File.Exists($"{debianLibDirectory}/libcuda.so.1") ||
            File.Exists($"{rpmLibDirectory}/libcuda.so.1"))
        {
            var gpuName = TryRunCli("nvidia-smi", "--query-gpu=name --format=csv,noheader,nounits");
            return new(true, gpuName is not null ? $"CUDA ({gpuName})" : "CUDA (NVIDIA)");
        }

        if (File.Exists($"{debianLibDirectory}/libvulkan_radeon.so") ||
            File.Exists($"{debianLibDirectory}/libdrm_amdgpu.so.1"))
        {
            var gpuName = TryRunCli("rocm-smi", "--showproductname");
            return new(true, gpuName is not null ? $"Vulkan ({gpuName})" : "Vulkan (AMD)");
        }

        if (File.Exists($"{debianLibDirectory}/libvulkan_intel.so"))
            return new(true, "Vulkan / OpenVINO (Intel)");

        return new(false, "CPU");
    }

    private static string? TryRunCli(string executable, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(executable, arguments)
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (process is null) return null;
            var firstOutputLine = process.StandardOutput.ReadLine()?.Trim();
            process.WaitForExit(3000);
            return string.IsNullOrEmpty(firstOutputLine) ? null : firstOutputLine;
        }
        catch
        {
            return null;
        }
    }
}