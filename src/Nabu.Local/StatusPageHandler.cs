using Nabu.Core.Config;
using Nabu.Core.Hardware;
using Nabu.Core.Models;

namespace Nabu.Local;

internal static class StatusPageHandler
{
    public static IResult GetStatusPage(
        GpuInfo gpuInfo,
        LoadedModelInfo loadedModel,
        WhisperLocalOptions options)
    {
        var rid = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
        var os = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
        var api = loadedModel.Mode;

        var statusHtmlPath = Path.Combine(AppContext.BaseDirectory, options.StatusHtmlPath);
        var html = File.Exists(statusHtmlPath)
            ? File.ReadAllText(statusHtmlPath)
                .Replace("{{Api}}", api)
                .Replace("{{Os}}", os)
                .Replace("{{Rid}}", rid)
                .Replace("{{Model}}", loadedModel.DisplayName)
            : $"<!DOCTYPE html><html><body><h1>Nabu.Local</h1><p>Graphics API: {api}</p><p>Platform: {os}</p><p>Runtime ID: {rid}</p><p>Model: {loadedModel.DisplayName}</p></body></html>";

        return Results.Content(html, "text/html");
    }
}
