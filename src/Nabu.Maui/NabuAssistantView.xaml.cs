namespace Nabu.Maui;

public partial class NabuAssistantView : ContentView
{
    // ── Bindable properties ────────────────────────────────────────────────

    public static readonly BindableProperty ShowLanguageSelectProperty =
        BindableProperty.Create(nameof(ShowLanguageSelect), typeof(bool),
            typeof(NabuAssistantView), defaultValue: true);

    /// <summary>Show or hide the language picker. Default: <c>true</c>.</summary>
    public bool ShowLanguageSelect
    {
        get => (bool)GetValue(ShowLanguageSelectProperty);
        set => SetValue(ShowLanguageSelectProperty, value);
    }

    // ── Events ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired on the UI thread when a final transcription is ready.
    /// Equivalent to <c>INabuHandler.OnTranscriptionReadyAsync</c> in Blazor.
    /// </summary>
    public event EventHandler<string>? TranscriptionReady;

    // ── Language list ──────────────────────────────────────────────────────

    private static readonly (string Display, string Whisper)[] Languages =
    [
        ("English",    "english"),
        ("German",     "german"),
        ("French",     "french"),
        ("Spanish",    "spanish"),
        ("Italian",    "italian"),
        ("Portuguese", "portuguese"),
        ("Chinese",    "chinese"),
        ("Japanese",   "japanese"),
        ("Korean",     "korean"),
        ("Hindi",      "hindi"),
    ];

    // ── Internals ──────────────────────────────────────────────────────────

    private NabuMauiService? _service;

    public NabuAssistantView()
    {
        InitializeComponent();
        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        _service = IPlatformApplication.Current!.Services
            .GetRequiredService<NabuMauiService>();

        WakeWordLabel.Text = $"Wake word: \"{_service.WakeWordPhrase}\"";

        // Populate language picker and restore stored selection
        LanguagePicker.ItemsSource = Languages.Select(l => l.Display).ToList();
        var stored = Preferences.Get("nabu.language", "english");
        var idx = Array.FindIndex(Languages, l => l.Whisper == stored);
        LanguagePicker.SelectedIndex = idx >= 0 ? idx : 0;

        SetIdleHint();

        _service.OnStatusChanged        += OnStatus;
        _service.OnWakeWordDetected     += OnWakeWord;
        _service.OnTranscriptionPreview += OnPreview;
        _service.OnTranscriptionFinal   += OnFinal;

        if (_service.IsInitialized)
            _service.Start();
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (_service == null) return;
        _service.Stop();
        _service.OnStatusChanged        -= OnStatus;
        _service.OnWakeWordDetected     -= OnWakeWord;
        _service.OnTranscriptionPreview -= OnPreview;
        _service.OnTranscriptionFinal   -= OnFinal;
    }

    private void OnLanguageChanged(object sender, EventArgs e)
    {
        if (_service == null || LanguagePicker.SelectedIndex < 0) return;
        var (_, whisper) = Languages[LanguagePicker.SelectedIndex];
        _service.SetLanguage(whisper);
        Preferences.Set("nabu.language", whisper);
    }

    // ── Pipeline callbacks — always on background thread ───────────────────

    private void OnStatus(string status) =>
        MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = status);

    private void OnWakeWord(string _) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            StatusLabel.Text = "Wake word detected…";
            PreviewLabel.Text = string.Empty;
        });

    private void OnPreview(string text) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            StatusLabel.Text = "Listening…";
            PreviewLabel.Text = text;
        });

    private void OnFinal(string text, string? _) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            FinalLabel.Text = text;
            SetIdleHint();
            StatusLabel.Text = "Idle.";
            TranscriptionReady?.Invoke(this, text);
        });

    private void SetIdleHint() =>
        PreviewLabel.Text = _service != null
            ? $"Say \"{_service.WakeWordPhrase}\" to start…"
            : string.Empty;
}
