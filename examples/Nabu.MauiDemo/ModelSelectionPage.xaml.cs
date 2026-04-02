using Nabu.Core.Hardware;
using Nabu.Maui;

namespace Nabu.MauiDemo;

public partial class ModelSelectionPage : ContentPage
{
    private GpuInfo? _gpu;
    private ModelViewModel? _selectedModel;
    private bool _forceCpu;
    private bool _busy;

    private NabuMauiService NabuService =>
        IPlatformApplication.Current!.Services.GetRequiredService<NabuMauiService>();

    public ModelSelectionPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // If a model was already set up, skip straight to MainPage
        var storedPath = Preferences.Get("nabu.model.path", string.Empty);
        var storedForceCpu = Preferences.Get("nabu.model.forcecpu", false);
        if (!string.IsNullOrEmpty(storedPath) && File.Exists(storedPath))
        {
            SetStatus("Initializing model…");
            try
            {
                await NabuService.InitializeAsync(storedPath, storedForceCpu);
                await Shell.Current.GoToAsync(nameof(MainPage));
            }
            catch (Exception ex)
            {
                SetStatus($"Failed to load model: {ex.Message}");
            }
            return;
        }

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        SetStatus("Detecting hardware…");
        _gpu = await Task.Run(MauiModelManager.DetectGpu);
        GpuLabel.Text = _gpu.IsGpu
            ? $"{_gpu.Label}  |  VRAM: {_gpu.VramFreeMb} MB free / {_gpu.VramTotalMb} MB total"
            : "No supported GPU detected — CPU mode will be used";

        var entries = MauiModelManager.GetEntries(_gpu);
        var vms = entries.Select(e => new ModelViewModel(e)).ToList();
        ModelList.ItemsSource = vms;

        // Pre-select the recommended model
        var recommended = vms.FirstOrDefault(v => v.IsRecommended) ?? vms.First();
        ModelList.SelectedItem = recommended;

        StatusLabel.IsVisible = false;
    }

    private void OnModelSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedModel = e.CurrentSelection.FirstOrDefault() as ModelViewModel;
        UpdateStartButton();
    }

    private void OnForceCpuChanged(object sender, CheckedChangedEventArgs e)
    {
        _forceCpu = e.Value;
        UpdateStartButton();
    }

    private void UpdateStartButton()
    {
        StartButton.IsEnabled = _selectedModel != null && !_busy;
        if (_selectedModel == null) return;

        bool onDisk = File.Exists(MauiModelManager.GetModelPath(
            _selectedModel.Size, _gpu!, _forceCpu || _selectedModel.RequiresForceCpu));

        StartButton.Text = onDisk ? "Start" : "Download & Start";
    }

    private async void OnStartClicked(object sender, EventArgs e)
    {
        if (_selectedModel == null || _gpu == null || _busy) return;

        _busy = true;
        StartButton.IsEnabled = false;
        bool forceCpu = _forceCpu || _selectedModel.RequiresForceCpu;

        try
        {
            string modelPath;

            if (!File.Exists(MauiModelManager.GetModelPath(_selectedModel.Size, _gpu, forceCpu)))
            {
                DownloadProgress.IsVisible = true;
                SetStatus("Downloading model…");

                var progress = new Progress<double>(p =>
                    MainThread.BeginInvokeOnMainThread(() => DownloadProgress.Progress = p));

                SetStatus($"Downloading {_selectedModel.SizeLabel}…");

                modelPath = await MauiModelManager.EnsureModelAsync(
                    _selectedModel.Size, _gpu, forceCpu, progress);

                DownloadProgress.Progress = 1.0;
            }
            else
            {
                modelPath = MauiModelManager.GetModelPath(_selectedModel.Size, _gpu, forceCpu);
            }

            SetStatus($"Initializing {_selectedModel.SizeLabel}…");
            await NabuService.InitializeAsync(modelPath, forceCpu);

            Preferences.Set("nabu.model.path", modelPath);
            Preferences.Set("nabu.model.forcecpu", forceCpu);

            await Shell.Current.GoToAsync(nameof(MainPage));
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}");
            DownloadProgress.IsVisible = false;
            StartButton.IsEnabled = true;
        }
        finally
        {
            _busy = false;
        }
    }

    private void SetStatus(string text)
    {
        StatusLabel.Text = text;
        StatusLabel.IsVisible = true;
    }
}

/// <summary>View model for a single model entry in the selection list.</summary>
internal sealed class ModelViewModel(MauiModelEntry entry)
{
    public string Size => entry.Size;
    public string SizeLabel => char.ToUpper(entry.Size[0]) + entry.Size[1..];
    public string Description => entry.Description.Split('\t').Last().TrimStart('-', ' ');
    public string SizeInfo => $"GPU: {entry.GpuSize}  |  CPU (Q4): {entry.CpuSize}";
    public bool IsDownloaded => entry.IsDownloaded;
    public bool IsRecommended => entry.IsRecommended;
    public bool RequiresForceCpu => entry.RequiresForceCpu;
}
