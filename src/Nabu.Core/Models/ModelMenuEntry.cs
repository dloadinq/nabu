namespace Nabu.Core.Models;

/// <summary>
/// Represents a single entry in the Whisper model selection console menu.
/// </summary>
/// <param name="Size">Size key used as the command identifier (e.g., <c>"small"</c>).</param>
/// <param name="Label">Display label shown in the menu, including a description (e.g., <c>"small\t- balanced"</c>).</param>
/// <param name="BaseName">Base filename used to check for installed model files on disk.</param>
/// <param name="GpuSize">Human-readable approximate size of the full-precision GPU model (e.g., <c>"~488 MB"</c>).</param>
/// <param name="Q4Size">Human-readable approximate size of the Q4_0 quantised CPU model (e.g., <c>"~190 MB"</c>).</param>
public record ModelMenuEntry(string Size, string Label, string BaseName, string GpuSize, string Q4Size);