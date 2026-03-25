using System.Diagnostics;

namespace Nabu.Core.Hardware;

public static class ProcessHelper
{
    private const int WaitSeconds = 3;

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