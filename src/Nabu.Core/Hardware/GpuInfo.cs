namespace Nabu.Core.Hardware;

/// <summary>
/// Describes the detected compute device and its available VRAM, used to select an appropriate
/// Whisper model quantisation and determine whether GPU acceleration is viable.
/// </summary>
/// <param name="IsGpu"><c>true</c> if a supported GPU was detected; <c>false</c> for CPU-only systems.</param>
/// <param name="Label">Human-readable display label, e.g. <c>"CUDA (NVIDIA GeForce RTX 4090)"</c> or <c>"CPU"</c>.</param>
/// <param name="VramFreeMb">Currently free VRAM in megabytes, or <c>null</c> if not available.</param>
/// <param name="VramTotalMb">Total VRAM in megabytes, or <c>null</c> if not available.</param>
public record GpuInfo(bool IsGpu, string Label, long? VramFreeMb = null, long? VramTotalMb = null);