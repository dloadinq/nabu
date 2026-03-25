using Nabu.Core.Models;

namespace Nabu.Core.ModelSetup;

public static class ModelMenu
{
    private const int IndentWidth = 2;
    public static readonly string Indent = "".PadRight(IndentWidth);

    public static string? Prompt(
        ModelMenuEntry[] entries,
        string modelsDirectory,
        string? recommendedSize,
        HashSet<string>? unavailableSizes,
        long? vramFreeMb,
        long? vramTotalMb = null,
        string? gpuLabel = null,
        string? cpuName = null)
    {
        int startRow = Console.CursorTop;
        PrintEntries(entries, modelsDirectory, recommendedSize, unavailableSizes, vramFreeMb, vramTotalMb, gpuLabel,
            cpuName);
        int endRow = Console.CursorTop;

        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            var selected = ResolveKey(key, entries);

            if (selected == "?") continue;

            ClearLines(startRow, endRow);

            if (selected is null)
                return null;

            return selected;
        }
    }

    private static void PrintEntries(
        ModelMenuEntry[] entries,
        string modelsDirectory,
        string? recommendedSize,
        HashSet<string>? unavailableSizes,
        long? vramFreeMb,
        long? vramTotalMb = null,
        string? gpuLabel = null,
        string? cpuName = null)
    {
        Console.WriteLine("Select Whisper model:");

        if (gpuLabel is not null)
            Console.WriteLine($"{Indent}GPU: {gpuLabel}");

        if (vramFreeMb.HasValue)
        {
            var reservedMb = ModelCatalog.GetBufferMb(vramFreeMb.Value);
            Console.Write($"{Indent}VRAM: ");

            if (vramTotalMb.HasValue)
            {
                var pct = (int)(100.0 * vramFreeMb.Value / vramTotalMb.Value);
                Console.ForegroundColor = pct >= 70 ? ConsoleColor.Green
                    : pct >= 40 ? ConsoleColor.Yellow
                    : pct >= 20 ? ConsoleColor.DarkYellow
                    : ConsoleColor.Red;
                Console.Write($"{vramFreeMb} MB / {vramTotalMb} MB ({pct}%)");
                Console.ResetColor();
            }
            else
            {
                Console.Write($"{vramFreeMb} MB");
            }

            Console.WriteLine($"  ({reservedMb} MB reserved as buffer)");
        }

        if (cpuName is not null)
            Console.WriteLine($"{Indent}CPU: {cpuName}");

        Console.WriteLine();

        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            var isUnavailable = unavailableSizes?.Contains(entry.Size) == true;
            var installedTag = GetInstalledTag(entry, modelsDirectory);
            var isRecommended = string.Equals(entry.Size, recommendedSize, StringComparison.OrdinalIgnoreCase);

            var parts = entry.Label.Split('\t', 2);
            var label = parts[0].PadRight(8) + (parts.Length > 1 ? parts[1] : "").PadRight(28);
            var sizeInfo = $"(GPU {entry.GpuSize} / Q4 {entry.Q4Size})".PadRight(28);

            Console.Write($"{Indent}[{i + 1}] {label} {sizeInfo}");

            if (!string.IsNullOrEmpty(installedTag))
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write(installedTag);
                Console.ResetColor();
            }

            if (isUnavailable)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("  (CPU: Insufficient VRAM for GPU)");
                Console.ResetColor();
            }
            else if (isRecommended)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("  (recommended)");
                Console.ResetColor();
            }

            Console.WriteLine();
        }

        Console.WriteLine();
        Console.Write("Press 1-5 to select, Q or Esc to quit: ");
    }

    private static string GetInstalledTag(ModelMenuEntry entry, string modelsDirectory)
    {
        bool hasGpuModel = File.Exists(Path.Combine(modelsDirectory, $"{entry.BaseName}.bin"));
        bool hasQ4Model = File.Exists(Path.Combine(modelsDirectory, $"{entry.BaseName}-q4_0.bin"));

        return (hasGpuModel, hasQ4Model) switch
        {
            (true, true) => $"{Indent}[installed: GPU + Q4]",
            (true, false) => $"{Indent}[installed: GPU]",
            (false, true) => $"{Indent}[installed: Q4]",
            _ => "",
        };
    }

    private static string? ResolveKey(ConsoleKeyInfo key, ModelMenuEntry[] entries)
    {
        int index = key.Key switch
        {
            ConsoleKey.D1 or ConsoleKey.NumPad1 => 0,
            ConsoleKey.D2 or ConsoleKey.NumPad2 => 1,
            ConsoleKey.D3 or ConsoleKey.NumPad3 => 2,
            ConsoleKey.D4 or ConsoleKey.NumPad4 => 3,
            ConsoleKey.D5 or ConsoleKey.NumPad5 => 4,
            ConsoleKey.Q or ConsoleKey.Escape => -1,
            _ => -2,
        };

        if (index == -2) return "?";
        if (index == -1) return null;
        if (index >= entries.Length) return "?";

        return entries[index].Size;
    }

    private static void ClearLines(int fromRow, int toRow)
    {
        try
        {
            var blankLine = new string(' ', Math.Max(Console.WindowWidth - 1, 1));
            for (int row = fromRow; row <= toRow; row++)
            {
                Console.SetCursorPosition(0, row);
                Console.Write(blankLine);
            }

            Console.SetCursorPosition(0, fromRow);
        }
        catch
        {
        }
    }
}