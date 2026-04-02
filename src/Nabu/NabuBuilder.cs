using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Nabu;

/// <summary>
/// Fluent builder returned by <see cref="NabuServiceCollectionExtensions.AddNabu"/> that configures
/// the Nabu voice assistant services in the dependency injection container.
/// </summary>
public class NabuBuilder
{
    private readonly IServiceCollection _services;
    private readonly List<CommandDefinitionItem> _definitions = [];

    /// <summary>
    /// Initialises the builder and registers the core Nabu services:
    /// <see cref="CommandDefinitionsProvider"/>, <see cref="VoiceCommandRegistry"/>, and
    /// the default <see cref="VoiceRegistryCommandHandler"/>.
    /// </summary>
    /// <param name="services">The application's service collection.</param>
    public NabuBuilder(IServiceCollection services)
    {
        _services = services;
        services.AddSingleton(new CommandDefinitionsProvider(_definitions));
        services.AddScoped<VoiceCommandRegistry>();
        services.AddScoped<INabuCommandHandler, VoiceRegistryCommandHandler>();
    }

    /// <summary>
    /// Registers a scoped <see cref="INabuHandler"/> implementation that receives raw transcription text.
    /// The same instance is resolved both by its concrete type and through the <see cref="INabuHandler"/> interface.
    /// </summary>
    /// <typeparam name="THandler">The concrete handler type to register.</typeparam>
    /// <returns>The current builder instance for method chaining.</returns>
    public NabuBuilder AddHandler<THandler>() where THandler : class, INabuHandler
    {
        _services.AddScoped<THandler>();
        _services.AddScoped<INabuHandler>(sp => sp.GetRequiredService<THandler>());
        return this;
    }

    /// <summary>
    /// Registers a scoped <see cref="INabuCommandHandler"/> implementation that receives resolved
    /// voice command identifiers. Multiple handlers can be registered; all will be invoked.
    /// </summary>
    /// <typeparam name="THandler">The concrete command handler type to register.</typeparam>
    /// <returns>The current builder instance for method chaining.</returns>
    public NabuBuilder AddCommandHandler<THandler>() where THandler : class, INabuCommandHandler
    {
        _services.AddScoped<THandler>();
        _services.AddScoped<INabuCommandHandler>(sp => sp.GetRequiredService<THandler>());
        return this;
    }

    /// <summary>
    ///     Registers the built-in navigation command handler.
    ///     Maps spoken keywords to app routes. "Back" and "previous" always trigger history.back().
    ///     The "navigate" command phrases are added automatically.
    /// </summary>
    public NabuBuilder AddNavigation(Action<NabuNavigationBuilder> configure)
    {
        var builder = new NabuNavigationBuilder();
        configure(builder);
        var routes = builder.Build();
        _services.AddScoped<NabuNavigationHandler>();
        _services.AddScoped<INabuCommandHandler>(sp => sp.GetRequiredService<NabuNavigationHandler>());
        AddCommand(NabuNavigationHandler.CommandPrefix + "back", NabuNavigationHandler.BackPhrases);
        foreach (var (route, keywords) in routes.GroupedRoutes)
            AddCommand(NabuNavigationHandler.CommandPrefix + route, NabuNavigationHandler.BuildRoutePhrases(keywords), excludeScope: route);
        return this;
    }

    /// <summary>
    ///     Registers a voice command with one or more natural language phrases.
    ///     The embedding model maps spoken input to the closest matching phrase via cosine similarity.
    ///     Synonyms and paraphrases work automatically without explicit keyword lists.
    /// </summary>
    /// <param name="id">
    ///     Unique command identifier. Used to dispatch to the matching <see cref="INabuCommandHandler"/>
    ///     or <see cref="VoiceCommandRegistry"/> callback.
    /// </param>
    /// <param name="descriptions">
    ///     Natural language phrases that should trigger this command, e.g. <c>["add one", "count up", "increment"]</c>.
    ///     More phrases = better coverage. Include synonyms and common paraphrases.
    /// </param>
    /// <param name="scope">
    ///     Optional route prefix. When set, this command is only considered when the user is on a matching route.
    ///     Example: <c>scope: "/counter"</c> restricts the command to the <c>/counter</c> page and its sub-pages.
    /// </param>
    /// <remarks>
    ///     Alternatively, load commands from an embedded JSON file using <see cref="AddCommandsFromResource"/>.
    ///     Both variants can be combined freely.
    /// </remarks>
    public NabuBuilder AddCommand(string id, string[] descriptions, string? scope = null, string? excludeScope = null)
    {
        foreach (var description in descriptions)
        {
            _definitions.Add(new CommandDefinitionItem(id, description,
                CommandDefinitionsProvider.Hash(id, description, scope, excludeScope), scope, excludeScope));
        }
        return this;
    }

    /// <summary>
    ///     Loads commands from a JSON file embedded as an assembly resource.
    ///     Supports two formats per command ID:
    /// </summary>
    /// <remarks>
    ///     <para><b>Simple format</b> (no scope):</para>
    ///     <code>
    ///     {
    ///       "increment": ["add one", "count up", "increment"]
    ///     }
    ///     </code>
    ///     <para><b>Extended format</b> (with scope):</para>
    ///     <code>
    ///     {
    ///       "increment": {
    ///         "scope": "/counter",
    ///         "phrases": ["add one", "count up", "increment"]
    ///       }
    ///     }
    ///     </code>
    ///     <para>
    ///         Embed the file in the calling project's <c>.csproj</c>:
    ///         <c>&lt;EmbeddedResource Include="commands.json" /&gt;</c>
    ///     </para>
    ///     <para>Both formats can be mixed in the same file. Can be combined with <see cref="AddCommand"/>.</para>
    /// </remarks>
    public NabuBuilder AddCommandsFromResource(string resourceName)
    {
        var assembly = Assembly.GetCallingAssembly();

        var actualResourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(r => 
                r.Equals(resourceName, StringComparison.OrdinalIgnoreCase) ||
                r.EndsWith("." + resourceName, StringComparison.OrdinalIgnoreCase));
        
        if (actualResourceName == null)
        {
            var available = string.Join(", ", assembly.GetManifestResourceNames());
            throw new FileNotFoundException($"Resource '{resourceName}' not found. Available: {available}");
        }

        using var stream = assembly.GetManifestResourceStream(actualResourceName);
        using var reader = new StreamReader(stream!);
        var json = reader.ReadToEnd();

        var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        if (raw == null) return this;

        foreach (var (id, element) in raw)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                var phrases = element.Deserialize<string[]>() ?? [];
                AddCommand(id, phrases);
            }
            else if (element.ValueKind == JsonValueKind.Object)
            {
                var scope = element.TryGetProperty("scope", out var s)
                    ? s.GetString()
                    : null;
                var phrases = element.TryGetProperty("phrases", out var p)
                    ? p.Deserialize<string[]>() ?? []
                    : [];
                AddCommand(id, phrases, scope);
            }
        }

        return this;
    }
}