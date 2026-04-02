namespace Nabu.Core.Hardware;

/// <summary>
/// Represents a point-in-time snapshot of VRAM availability for a GPU device.
/// Both values may be <c>null</c> when the information cannot be determined on the current platform.
/// </summary>
/// <param name="FreeMb">Currently free (unallocated) VRAM in megabytes.</param>
/// <param name="TotalMb">Total installed VRAM in megabytes.</param>
public record VramInfo(long? FreeMb, long? TotalMb);