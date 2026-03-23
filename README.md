# Nabu

Razor Class Library for Whisper speech-to-text in Blazor apps. Supports browser (WebGPU/WASM) and server backends.

## Features

- **Dual-backend**: Automatically switches between browser inference (WebGPU/WASM) and server inference (Whisper.net via SignalR)
- **Live preview**: Real-time transcription text while recording
- **Voice Activity Detection**: Silero VAD (ONNX)
- **Wake-word detection**: Optional via NanoWakeWord
- **50+ languages** supported
- Compatible with **Blazor Server** and **Razor Pages**

---

## Installation

Nabu is not yet published as a NuGet package. Add it as a project reference:

```xml
<!-- MyApp.csproj -->
<ItemGroup>
  <ProjectReference Include="../Nabu.RCL/Nabu.RCL.csproj" />
</ItemGroup>
```

---

## Blazor Server

### 1. Register services

```csharp
// Program.cs
using Nabu.RCL;

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddNabu()
    .AddHandler<MyWhisperHandler>();
```

### 2. Implement `IWhisperHandler`

```csharp
using Nabu.RCL;

public class MyWhisperHandler : IWhisperHandler
{
    public Task OnTranscriptionReadyAsync(string text)
    {
        // text is never empty or whitespace.
        // Filter WhisperConstants.TerminationText / NoRecognizableSpeechText if needed.
        Console.WriteLine($"Transcription: {text}");
        return Task.CompletedTask;
    }
}
```

### 3. Add `WhisperWidget` to a page

```razor
@page "/"
@rendermode InteractiveServer
@inject IWhisperSettings Settings

<WhisperWidget ShowLanguageSelect="true" />

<p>Status: @Settings.Status</p>
<p>Backend: @Settings.Backend</p>
```

---

## Razor Pages

### 1. Register services

```csharp
// Program.cs
using Nabu.RCL;

builder.Services.AddRazorPages();

builder.Services.AddNabu()
    .AddHandler<MyWhisperHandler>();
```

### 2. Add `WhisperViewComponent` to a page

```html
<!-- Index.cshtml -->
<component type="typeof(WhisperViewComponent)" render-mode="Static" param-ShowLanguageSelect="true" />

<script>
    window.addEventListener('whisper:transcriptionFinal', event => {
        const text = (event.detail || '').trim();
        if (!text) return;

        fetch('?handler=Transcription', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ text })
        });
    });
</script>
```

```csharp
// Index.cshtml.cs
public class IndexModel : PageModel
{
    public async Task<IActionResult> OnPostTranscriptionAsync([FromBody] TranscriptionDto dto)
    {
        // handle dto.Text
        return new JsonResult(new { ok = true });
    }

    public record TranscriptionDto(string Text);
}
```

---

## Server backend (Nabu.Local)

For server-side inference via Whisper.net, run `Nabu.Local` as a separate service.

### Start

```bash
cd Nabu.Local
dotnet run
# Listens on http://localhost:50000 by default
```

### Configuration (`appsettings.json`)

```json
{
  "WhisperLocal": {
    "Url": "http://localhost:50000",
    "Whisper": {
      "GpuModelPath": "models/ggml-medium.bin",
      "CpuModelPath": "models/ggml-medium_q4.bin",
      "Language": "english"
    },
    "Vad": {
      "Threshold": 0.75,
      "MinSpeechDurationMs": 250,
      "MinSilenceDurationMs": 1200
    },
    "WakeWord": {
      "Model": "hey_jarvis_v0.1",
      "Threshold": 0.4
    }
  }
}
```

If no server is reachable, Nabu falls back to browser inference automatically.

---

## CSS customization

Override the overlay appearance with CSS custom properties:

```css
:root {
    --speech-overlay-bg: rgba(0, 0, 0, 0.65);
    --speech-dialog-bg: rgba(20, 20, 40, 0.95);
    --speech-dialog-radius: 24px;
    --speech-dialog-max-width: 520px;
    --speech-highlight-color: #4ade80;   /* "Done." text */
    --speech-visualizer-height: 280px;
    --speech-send-btn-bg: #16a34a;
}
```

All available variables are listed in `Nabu.RCL/wwwroot/css/speech-overlay.css`.

---

## Component parameters

### `WhisperWidget` / `WhisperViewComponent`

| Parameter            | Type   | Default | Description                    |
|----------------------|--------|---------|--------------------------------|
| `ShowLanguageSelect` | `bool` | `false` | Show language selection dropdown |

---

## Supported languages

Popular: English, German, French, Spanish, Italian, Portuguese, Chinese, Japanese, Korean, Hindi

~45 additional languages are available via `WhisperLanguages.Extended`.

---

## Project structure

```
Nabu.RCL/            # Razor Class Library (main library)
Nabu.Local/          # Optional server service (Whisper.net + SignalR)
Nabu.BlazorDemo/     # Demo: Blazor Server
Nabu.RazorPagesDemo/ # Demo: Razor Pages
external/
  silero-vad/        # Voice Activity Detection
  NanoWakeWord/      # Wake-word detection
```
