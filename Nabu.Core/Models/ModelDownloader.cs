using System.Diagnostics;

namespace Nabu.Core.Models;

internal record SpeedSample(long ElapsedMs, long BytesDownloaded);

internal static class ModelDownloader
{
    private const int BarWidth = 28;

    public static async Task DownloadAsync(Stream source, Stream destination, long expectedTotalBytes)
    {
        var buffer = new byte[81920];
        long downloaded = 0;
        int bytesRead;

        var speedWindow = new Queue<SpeedSample>();
        var stopwatch = Stopwatch.StartNew();

        while ((bytesRead = await source.ReadAsync(buffer)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead));
            downloaded += bytesRead;

            long elapsedMs = stopwatch.ElapsedMilliseconds;
            UpdateSpeedWindow(speedWindow, elapsedMs, downloaded);

            double bytesPerSecond = ComputeSpeed(speedWindow, downloaded, elapsedMs);
            Console.Write(BuildProgressLine(downloaded, expectedTotalBytes, bytesPerSecond));
        }
    }

    private static string BuildProgressLine(long downloaded, long totalBytes, double bytesPerSecond)
    {
        var bar = BuildBar(downloaded, totalBytes);
        var size = FormatSize(downloaded, totalBytes);
        var speed = FormatSpeed(bytesPerSecond);
        var eta = bytesPerSecond > 0 && totalBytes > 0
            ? $"  ETA {FormatEta((totalBytes - downloaded) / bytesPerSecond)}"
            : "";

        return $"\r[{bar}]\t{size}  {speed}{eta}   ";
    }

    private static string BuildBar(long downloaded, long totalBytes)
    {
        int filledCount = totalBytes > 0
            ? (int)Math.Min(BarWidth, downloaded * BarWidth / totalBytes)
            : 0;

        var barCharacters = new string('=', filledCount).PadRight(BarWidth).ToCharArray();
        OverlayPercent(barCharacters, downloaded, totalBytes);
        return new string(barCharacters);
    }

    private static void OverlayPercent(char[] barCharacters, long downloaded, long totalBytes)
    {
        if (totalBytes <= 0) return;
        var percentageLabel = $"{downloaded * 100 / totalBytes}%";
        int centerPosition = (BarWidth - percentageLabel.Length) / 2;
        for (int i = 0; i < percentageLabel.Length; i++)
            barCharacters[centerPosition + i] = percentageLabel[i];
    }

    private static string FormatSize(long downloaded, long totalBytes)
    {
        var downloadedMb = downloaded / 1_048_576.0;
        return totalBytes > 0
            ? $"{downloadedMb:F0} MB / {totalBytes / 1_048_576} MB"
            : $"{downloadedMb:F0} MB";
    }

    private static string FormatSpeed(double bytesPerSecond) => bytesPerSecond switch
    {
        >= 1_048_576 => $"{bytesPerSecond / 1_048_576:F1} MB/s",
        >= 1_024 => $"{bytesPerSecond / 1_024:F0} KB/s",
        _ => "--",
    };

    private static string FormatEta(double seconds) => seconds switch
    {
        >= 3600 => $"{(int)(seconds / 3600)}h {(int)(seconds % 3600 / 60):D2}m",
        >= 60 => $"{(int)(seconds / 60)}m {(int)(seconds % 60):D2}s",
        _ => $"{(int)seconds}s",
    };

    private static void UpdateSpeedWindow(Queue<SpeedSample> speedWindow, long elapsedMs, long downloaded)
    {
        speedWindow.Enqueue(new SpeedSample(elapsedMs, downloaded));
        while (speedWindow.Count > 1 && elapsedMs - speedWindow.Peek().ElapsedMs > 5_000)
            speedWindow.Dequeue();
    }

    private static double ComputeSpeed(Queue<SpeedSample> speedWindow, long downloaded, long elapsedMs)
    {
        if (speedWindow.Count < 2) return 0;
        var oldestSample = speedWindow.Peek();
        double windowMs = elapsedMs - oldestSample.ElapsedMs;
        return windowMs > 0 ? (downloaded - oldestSample.BytesDownloaded) / windowMs * 1000.0 : 0;
    }
}