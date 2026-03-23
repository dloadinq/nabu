namespace Nabu.RCL;

/// <summary>
/// Central settings for the Whisper widget (language, status, backend).
/// Use this to change language or display status/backend from a dedicated settings page (e.g. Home).
/// </summary>
public interface IWhisperSettings
{
    string Language { get; set; }
    string Status { get; set; }
    string Backend { get; set; }

    event Action? LanguageChanged;
    event Action? StatusChanged;
    event Action? BackendChanged;
}
