namespace Nabu;

/// <summary>
/// Provides access to the central configuration and reactive state of the Nabu Assistant.
/// Inject this to synchronize UI elements with the current voice assistant status.
/// </summary>
public interface INabuSettings
{
    /// <summary>Gets or sets the current transcription language (e.g., "english", "german").</summary>
    string Language { get; set; }

    /// <summary>Gets the human-readable status message (e.g., "Listening...", "Transcribing...").</summary>
    string Status { get; set; }

    /// <summary>Gets the name of the active inference engine (e.g., "Browser (WebGPU)" or "Remote Server").</summary>
    string Backend { get; set; }

    /// <summary>Fires when the <see cref="Language"/> property has been updated.</summary>
    event Action? LanguageChanged;

    /// <summary>Fires when the <see cref="Status"/> message changes during the voice lifecycle.</summary>
    event Action? StatusChanged;

    /// <summary>Fires when the system switches between local and remote backends.</summary>
    event Action? BackendChanged;
}
