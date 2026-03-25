namespace Nabu.Core.Hardware;

public record GpuInfo(bool IsGpu, string Label, long? VramFreeMb = null, long? VramTotalMb = null);