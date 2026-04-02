namespace Nabu.Core.Models;

/// <summary>
/// Describes the Whisper model that is currently loaded and ready for inference.
/// </summary>
/// <param name="DisplayName">
/// Human-readable model name shown in status outputs, e.g. <c>"Small (no quantization)"</c> or <c>"Large (q4_0)"</c>.
/// </param>
/// <param name="Mode">
/// Inference mode label, e.g. <c>"CUDA (RTX 4090)"</c>, <c>"CPU"</c>, or <c>"CPU: Insufficient VRAM"</c>.
/// </param>
public record LoadedModelInfo(string DisplayName, string Mode);