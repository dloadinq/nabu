using Nabu.Maui;

namespace Nabu.MauiDemo;

public partial class MainPage : ContentPage
{
    private readonly NabuMauiService _nabu;

    public MainPage(NabuMauiService nabu)
    {
        InitializeComponent();
        _nabu = nabu;
    }

    private void OnTranscriptionReady(object? sender, string text)
    {
        // Handle final transcription here — e.g. send to a chat, execute a command, etc.
        System.Diagnostics.Debug.WriteLine($"[Nabu] Transcription: {text}");
    }

    private async void OnChangeModelClicked(object sender, EventArgs e)
    {
        _nabu.Stop();
        Preferences.Remove("nabu.model.path");
        Preferences.Remove("nabu.model.forcecpu");
        await Shell.Current.GoToAsync("..");
    }
}
