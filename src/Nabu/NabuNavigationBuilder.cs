namespace Nabu;

/// <summary>
/// Fluent builder for configuring voice-driven navigation routes.
/// Passed to the <c>configure</c> delegate of <see cref="NabuBuilder.AddNavigation"/>.
/// </summary>
public sealed class NabuNavigationBuilder
{
    private readonly Dictionary<string, string> _routes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Maps one or more spoken keywords to an application route.
    /// For example, <c>Map("/counter", "counter", "counting page")</c> causes any of those keywords to
    /// navigate the user to <c>/counter</c>.
    /// </summary>
    /// <param name="route">The target route path (e.g., <c>"/counter"</c>).</param>
    /// <param name="keywords">
    /// One or more natural language keywords. Each keyword is registered as a separate voice phrase,
    /// and the embedding model will also match paraphrases automatically.
    /// </param>
    /// <returns>The current builder instance for method chaining.</returns>
    public NabuNavigationBuilder Map(string route, params string[] keywords)
    {
        foreach (var keyword in keywords)
        {
            _routes[keyword] = route;
        }
        return this;
    }

    /// <summary>
    /// Builds a <see cref="NabuNavigationRoutes"/> instance from the accumulated keyword-to-route mappings.
    /// </summary>
    /// <returns>A routes object consumed by <see cref="NabuBuilder.AddNavigation"/>.</returns>
    public NabuNavigationRoutes Build()
    {
        return new (_routes);
    }
}