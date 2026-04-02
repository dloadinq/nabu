namespace Nabu;

/// <summary>
/// Provides the language lists used by the Nabu language selector.
/// Keys are the Whisper language identifiers (lowercase); values are the display names shown in the UI.
/// </summary>
public static class NabuLanguages
{
    /// <summary>
    /// The most widely used languages, shown by default in the language picker without requiring the user
    /// to expand the full list.
    /// </summary>
    public static readonly Dictionary<string, string> Popular = new(StringComparer.OrdinalIgnoreCase)
    {
        ["english"] = "English",
        ["german"] = "German",
        ["french"] = "French",
        ["spanish"] = "Spanish",
        ["italian"] = "Italian",
        ["portuguese"] = "Portuguese",
        ["chinese"] = "Chinese",
        ["japanese"] = "Japanese",
        ["korean"] = "Korean",
        ["hindi"] = "Hindi",
    };

    /// <summary>
    /// Additional languages revealed when the user expands the language picker.
    /// Covers European, Middle Eastern, South Asian, and South-East Asian languages.
    /// </summary>
    public static readonly Dictionary<string, string> Extended = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dutch"] = "Dutch",
        ["danish"] = "Danish",
        ["swedish"] = "Swedish",
        ["norwegian"] = "Norwegian",
        ["finnish"] = "Finnish",
        ["icelandic"] = "Icelandic",
        ["irish"] = "Irish",
        ["welsh"] = "Welsh",
        ["scottish gaelic"] = "Scottish Gaelic",
        ["polish"] = "Polish",
        ["czech"] = "Czech",
        ["slovak"] = "Slovak",
        ["hungarian"] = "Hungarian",
        ["romanian"] = "Romanian",
        ["bulgarian"] = "Bulgarian",
        ["croatian"] = "Croatian",
        ["serbian"] = "Serbian",
        ["slovenian"] = "Slovenian",
        ["bosnian"] = "Bosnian",
        ["macedonian"] = "Macedonian",
        ["lithuanian"] = "Lithuanian",
        ["latvian"] = "Latvian",
        ["estonian"] = "Estonian",
        ["russian"] = "Russian",
        ["ukrainian"] = "Ukrainian",
        ["belarusian"] = "Belarusian",
        ["greek"] = "Greek",
        ["albanian"] = "Albanian",
        ["maltese"] = "Maltese",
        ["turkish"] = "Turkish",
        ["arabic"] = "Arabic",
        ["hebrew"] = "Hebrew",
        ["persian"] = "Persian",
        ["urdu"] = "Urdu",
        ["vietnamese"] = "Vietnamese",
        ["thai"] = "Thai",
        ["indonesian"] = "Indonesian",
        ["malay"] = "Malay",
        ["tagalog"] = "Tagalog",
        ["swahili"] = "Swahili",
    };
}