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

### NuGet (recommended)

```bash
dotnet add package Nabu.RCL --version 1.0.0-preview.3
```

Or in your `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Nabu.RCL" Version="1.0.0-preview.3" />
</ItemGroup>
```

### Project reference (from source)

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
    .AddHandler<MyHandler>();
```

### 2. Add the SiriWave script to `App.razor`

```html
<!-- App.razor -->
<script src="https://cdn.jsdelivr.net/npm/siriwave/dist/siriwave.umd.min.js"></script>
```

Place this before the closing `</body>` tag.

### 3. Add the `@using` directive to `_Import.razor`

```razor
@using Nabu.RCL
```

### 4. Implement `INabuHandler`

```csharp
using Nabu.RCL;

public class MyHandler : INabuHandler
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

### 5. Add `Nabu` to a page

```razor
@page "/"

<Nabu ShowLanguageSelect="true" />
```

No `@rendermode` needed — the component sets `InteractiveServer` automatically.

---

## Razor Pages

### 1. Register services

```csharp
// Program.cs
using Nabu.RCL;

builder.Services.AddRazorPages();

builder.Services.AddNabu()
    .AddHandler<MyHandler>();
```

### 2. Add the SiriWave script to `_Layout.cshtml`

```html
<!-- _Layout.cshtml -->
<script src="https://cdn.jsdelivr.net/npm/siriwave/dist/siriwave.umd.min.js"></script>
```

Place this before the closing `</body>` tag.

### 3. Add the `@using` and `@addTagHelper` directives to `_ViewImports.cshtml`

```razor
@using Nabu.RCL
@addTagHelper *, Nabu.RCL
```

### 4. Add `<nabu>` to a page

```html
<!-- Index.cshtml -->
<nabu show-language-select="true"></nabu>

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

### `Nabu` / `NabuView`

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
  silero-vad/        # Voice Activity Detection (git submodule)
  NanoWakeWord/      # Wake-word detection (git submodule)
```

### Cloning with submodules

The `external/` dependencies are git submodules. Clone with:

```bash
git clone --recurse-submodules https://github.com/daniel-lerch/nabu.git
```

If you already cloned without `--recurse-submodules`, run:

```bash
git submodule update --init
```
