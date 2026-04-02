namespace Nabu.Core.Models;

/// <summary>
/// Captures the user's model selection from the console menu, including whether CPU-only inference
/// was explicitly requested.
/// </summary>
/// <param name="Size">The chosen model size key (e.g., <c>"small"</c>).</param>
/// <param name="ForceCpu">
/// <c>true</c> when the user selected CPU mode via the menu toggle, forcing use of the Q4_0 quantised
/// model even when a GPU with sufficient VRAM is available.
/// </param>
public record ModelSelection(string Size, bool ForceCpu);
