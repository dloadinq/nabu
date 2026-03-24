namespace Nabu.Core.Models;

internal static class ModelMenu
{
    public static string Prompt(ModelMenuEntry[] entries, string modelsDirectory)
    {
        int startRow = Console.CursorTop;
        PrintEntries(entries, modelsDirectory);
        int endRow = Console.CursorTop;

        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            var selected = ResolveKey(key, entries);

            if (selected == "?") continue;

            ClearLines(startRow, endRow);

            if (selected is null)
                Environment.Exit(0);

            Console.WriteLine($"Model: {selected}");
            return selected;
        }
    }

    private static void PrintEntries(ModelMenuEntry[] entries, string modelsDirectory)
    {
        Console.WriteLine();
        Console.WriteLine("Select Whisper model:");
        Console.WriteLine();

        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            var installedTag = GetInstalledTag(entry, modelsDirectory);
            Console.WriteLine($"  [{i + 1}] {entry.Label,-38} (GPU {entry.GpuSize} / Q4 {entry.Q4Size}){installedTag}");
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
            (true, true) => "  [installed: GPU + Q4]",
            (true, false) => "  [installed: GPU]",
            (false, true) => "  [installed: Q4]",
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
        return index < entries.Length ? entries[index].Size : "?";
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
        { }
    }
}