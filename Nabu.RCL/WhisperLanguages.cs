namespace Nabu.RCL;

/// <summary>
/// Static language definitions for the Whisper UI (WhisperWidget, WhisperViewComponent).
/// </summary>
public static class WhisperLanguages
{
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