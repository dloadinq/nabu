using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Nabu;

/// <summary>
/// Built-in <see cref="INabuCommandHandler"/> that translates navigation command identifiers into
/// Blazor route changes or browser history navigation.
/// Registered automatically when <see cref="NabuBuilder.AddNavigation"/> is called.
/// </summary>
public sealed class NabuNavigationHandler(
    NavigationManager navigationManager,
    IJSRuntime jsRuntime) : INabuCommandHandler
{
    /// <summary>
    /// Prefix applied to every navigation command identifier so that other handlers can easily
    /// ignore them (e.g., <c>"navigate:back"</c>, <c>"navigate:/counter"</c>).
    /// </summary>
    internal const string CommandPrefix = "navigate:";

    /// <summary>
    /// Natural language back-navigation phrases registered automatically with the back command.
    /// </summary>
    internal static readonly string[] BackPhrases =
    [
        "go back",
        "go back to the previous page",
        "navigate back",
        "previous page",
        "return to the previous page",
        "bring me back",
        "take me back",
    ];

    /// <summary>
    /// Generates navigation phrases from the keywords for a single route.
    /// e.g. keywords ["home", "homepage"] → "go to the home page", "take me to the homepage", …
    /// </summary>
    internal static string[] BuildRoutePhrases(IEnumerable<string> keywords)
    {
        var phrases = new List<string>();
        foreach (var keyword in keywords)
        {
            phrases.Add($"go to the {keyword} page");
            phrases.Add($"navigate to the {keyword} page");
            phrases.Add($"open the {keyword} page");
            phrases.Add($"show me the {keyword}");
            phrases.Add($"take me to the {keyword}");
            phrases.Add($"bring me to the {keyword}");
            phrases.Add($"lead me to the {keyword}");
            phrases.Add($"go to {keyword}");
            phrases.Add($"go {keyword}");
            phrases.Add($"take me {keyword}");
        }
        return [.. phrases];
    }

    /// <summary>
    /// Handles a resolved navigation command. Commands whose identifier does not start with
    /// <see cref="CommandPrefix"/> are ignored. The <c>"back"</c> target invokes
    /// <c>history.back()</c> in the browser; all other targets call
    /// <see cref="NavigationManager.NavigateTo"/>.
    /// </summary>
    /// <param name="commandId">The resolved command identifier (e.g., <c>"navigate:/counter"</c>).</param>
    /// <param name="englishText">The English translation of the original utterance (unused by this handler).</param>
    public Task OnCommandAsync(string commandId, string englishText)
    {
        if (!commandId.StartsWith(CommandPrefix, StringComparison.Ordinal))
            return Task.CompletedTask;

        var target = commandId[CommandPrefix.Length..];

        if (target == "back")
            return jsRuntime.InvokeVoidAsync("history.back").AsTask();

        navigationManager.NavigateTo(target);
        return Task.CompletedTask;
    }
}
