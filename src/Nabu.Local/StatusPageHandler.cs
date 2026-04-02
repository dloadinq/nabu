using System.Runtime.InteropServices;
using Nabu.Core.Config;
using Nabu.Core.Hardware;
using Nabu.Core.Models;

namespace Nabu.Local;

internal static class StatusPageHandler
{
    public static string BuildStatusPage(
        GpuInfo gpuInfo,
        LoadedModelInfo loadedModel,
        NabuLocalOptions options)
    {
        var runtimeIdentifierId = RuntimeInformation.RuntimeIdentifier;
        var operatingSystem = RuntimeInformation.OSDescription;
        var api = loadedModel.Mode;

        var statusHtmlPath = Path.Combine(AppContext.BaseDirectory, options.StatusHtmlPath);
        return File.Exists(statusHtmlPath)
            ? File.ReadAllText(statusHtmlPath)
                .Replace("{{Api}}", api)
                .Replace("{{Os}}", operatingSystem)
                .Replace("{{Rid}}", runtimeIdentifierId)
                .Replace("{{Model}}", loadedModel.DisplayName)
            : $"<!DOCTYPE html><html><body><h1>Nabu.Local</h1><p>Graphics API: {api}</p><p>Platform: {operatingSystem}</p><p>Runtime ID: {runtimeIdentifierId}</p><p>Model: {loadedModel.DisplayName}</p></body></html>";
    }
}
