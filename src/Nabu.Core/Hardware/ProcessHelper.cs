using System.Diagnostics;

namespace Nabu.Core.Hardware;

/// <summary>
/// Utility for running external command-line tools and capturing their first line of output.
/// Used by <see cref="GpuDetector"/> to query GPU names from tools such as <c>nvidia-smi</c> and <c>rocm-smi</c>.
/// </summary>
public static class ProcessHelper
{
    private const int WaitSeconds = 3;

    /// <summary>
    /// Starts <paramref name="executable"/> with the given <paramref name="arguments"/>, reads the first
    /// line of standard output, and waits up to <see cref="WaitSeconds"/> seconds for the process to exit.
    /// </summary>
    /// <param name="executable">The executable file to run (e.g., <c>"nvidia-smi"</c>).</param>
    /// <param name="arguments">Command-line arguments string passed to the process.</param>
    /// <returns>
    /// The trimmed first output line, or <c>null</c> if the process could not be started, produced no
    /// output, or threw an exception.
    /// </returns>
    public static string? RunFirstLine(string executable, string arguments)
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
            var line = process.StandardOutput.ReadLine()?.Trim();
            process.WaitForExit(WaitSeconds * 1000);
            return string.IsNullOrEmpty(line) ? null : line;
        }
        catch
        {
            return null;
        }
    }
}