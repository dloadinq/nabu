using Microsoft.Extensions.DependencyInjection;

namespace Nabu;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> that register the Nabu voice assistant services.
/// </summary>
public static class NabuServiceCollectionExtensions
{
    /// <summary>
    /// Registers the core Nabu services and returns a <see cref="NabuBuilder"/> for further configuration.
    /// </summary>
    /// <remarks>
    /// Call this method in <c>Program.cs</c> and chain additional builder calls to register handlers
    /// and voice commands:
    /// <code>
    /// builder.Services.AddNabu()
    ///     .AddHandler&lt;MyTranscriptionHandler&gt;()
    ///     .AddCommand("increment", ["add one", "count up"]);
    /// </code>
    /// </remarks>
    /// <param name="services">The application's service collection.</param>
    /// <returns>A <see cref="NabuBuilder"/> instance for fluent configuration.</returns>
    public static NabuBuilder AddNabu(this IServiceCollection services)
    {
        services.AddScoped<INabuSettings, NabuSettingsService>();
        return new NabuBuilder(services);
    }
}