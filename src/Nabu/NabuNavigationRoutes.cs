namespace Nabu;

/// <summary>
/// Immutable result produced by <see cref="NabuNavigationBuilder.Build"/> that stores the keyword-to-route
/// mappings used by <see cref="NabuBuilder.AddNavigation"/> to register navigation voice commands.
/// </summary>
public sealed class NabuNavigationRoutes
{
    private readonly Dictionary<string, string> _keywordToRoute;

    internal NabuNavigationRoutes(Dictionary<string, string> routes)
    {
        _keywordToRoute = routes;
    }

    /// <summary>
    /// Groups the keyword-to-route dictionary by route, yielding each unique route paired with all
    /// keywords that map to it. Used internally to register one command per route.
    /// </summary>
    internal IEnumerable<(string Route, IEnumerable<string> Keywords)> GroupedRoutes =>
        _keywordToRoute
            .GroupBy(kv => kv.Value)
            .Select(g => (g.Key, g.Select(kv => kv.Key)));
}
