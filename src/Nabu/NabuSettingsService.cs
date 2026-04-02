namespace Nabu;

/// <summary>
/// Default implementation of <see cref="INabuSettings"/>. 
/// Manages the reactive state of the Nabu widget and triggers events on property changes.
/// Registration: builder.Services.AddScoped&lt;INabuSettings, NabuSettingsService&gt;()
/// </summary>
/// <remarks>
/// This service should be registered as 'Scoped' to ensure each user circuit 
/// has its own independent state.
/// </remarks>
public class NabuSettingsService : INabuSettings
{
    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public string Backend
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            BackendChanged?.Invoke();
        }
    } = NabuConstants.BackendDetecting;

    /// <inheritdoc/>
    public event Action? LanguageChanged;

    /// <inheritdoc/>
    public event Action? StatusChanged;

    /// <inheritdoc/>
    public event Action? BackendChanged;
}