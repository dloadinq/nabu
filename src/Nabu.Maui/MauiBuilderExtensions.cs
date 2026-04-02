using Nabu.Core.Config;

namespace Nabu.Maui;

public static class MauiBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="NabuMauiService"/> and its options.
    /// Call <see cref="NabuMauiService.InitializeAsync"/> after model selection before starting.
    /// </summary>
    public static MauiAppBuilder UseNabu(
        this MauiAppBuilder builder,
        Action<NabuLocalOptions>? configure = null)
    {
        builder.Services.Configure<NabuLocalOptions>(opt => configure?.Invoke(opt));
        builder.Services.AddSingleton<NabuMauiService>();
        return builder;
    }
}
