namespace Nabu.RCL;

/// <summary>
/// Default implementation of <see cref="IWhisperSettings"/>.
/// Register as scoped (per circuit): builder.Services.AddScoped&lt;IWhisperSettings, WhisperSettingsService&gt;()
/// </summary>
public class WhisperSettingsService : IWhisperSettings
{
    public string Language
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            LanguageChanged?.Invoke();
        }
    } = "english";

    public string Status
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            StatusChanged?.Invoke();
        }
    } = "";

    public string Backend
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            BackendChanged?.Invoke();
        }
    } = WhisperConstants.BackendDetecting;

    public event Action? LanguageChanged;
    public event Action? StatusChanged;
    public event Action? BackendChanged;
}
