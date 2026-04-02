# Nabu

Razor Class Library for Whisper speech-to-text in Blazor apps. Supports browser (WebGPU/WASM) and server backends.

## Getting Started

### Cloning the Project
This project uses Git submodules for heavy dependencies. Use the following command to clone everything:

```bash
git clone --recurse-submodules https://github.com/dloadinq/nabu.git
cd nabu
```

### Optimizing Submodules (Sparse-Checkout)
To avoid downloading unnecessary files and to prevent build conflicts with Native AOT (e.g., in `silero-vad`), use **Sparse-Checkout**. After cloning, run these commands:

```bash
# Setup Silero VAD to only include C# examples and the ONNX model
cd external/silero-vad
git sparse-checkout set --no-cone "/examples/csharp/*" "/src/silero_vad/data/silero_vad.onnx"
cd ../..

# Setup NanoWakeWord (only the core library folder)
cd external/NanoWakeWord
git sparse-checkout set --no-cone "/NanoWakeWord/*"
cd ../..
```

---

## Features

- **Dual-backend**: Automatically switches between browser inference (WebGPU/WASM) and server inference (Whisper.net via SignalR)
- **Live preview**: Real-time transcription text while recording
- **Voice Activity Detection**: Silero VAD (ONNX)
- **Wake-word detection**: Optional via NanoWakeWord
- **Voice commands**: Semantic command matching with scoping and JSON/code registration
- **Voice navigation**: Automatic route-based voice navigation
- **50+ languages** supported
- Compatible with **Blazor Server**, **Blazor WebAssembly**, **Razor Pages**, and **.NET MAUI**

---

## Installation

### NuGet (recommended)

**Blazor Server / Blazor WebAssembly:**
```bash
dotnet add package Nabu --version 1.0.0-preview.4
```

**Razor Pages (also includes Blazor support):**
```bash
dotnet add package Nabu.Server --version 1.0.0-preview.4
```

Or in your `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Nabu" Version="1.0.0-preview.4" />
  <!-- or for Razor Pages: -->
  <PackageReference Include="Nabu.Server" Version="1.0.0-preview.4" />
</ItemGroup>
```

### Project reference (from source)

```xml
<ItemGroup>
  <!-- Blazor apps -->
  <ProjectReference Include="../src/Nabu/Nabu.csproj" />
  <!-- Razor Pages apps -->
  <ProjectReference Include="../src/Nabu.Server/Nabu.Server.csproj" />
</ItemGroup>
```

---

## Blazor Server

### 1. Register services

```csharp
// Program.cs
using Nabu;

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddNabu()
    .AddHandler<MyHandler>();
```

### 2. Add the SiriWave script to `App.razor`

```html
<script src="https://cdn.jsdelivr.net/npm/siriwave/dist/siriwave.umd.min.js"></script>
```

Place this before the closing `</body>` tag.

### 3. Add the `@using` directive to `_Import.razor`

```razor
@using Nabu
```

### 4. Implement `INabuHandler`

```csharp
using Nabu;

public class MyHandler : INabuHandler
{
    public Task OnTranscriptionReadyAsync(string text)
    {
        // text is never empty or whitespace.
        // Filter NabuConstants.TerminationText / NoRecognizableSpeechText if needed.
        Console.WriteLine($"Transcription: {text}");
        return Task.CompletedTask;
    }
}
```

### 5. Add `<NabuAssistant>` to a page

```razor
@page "/"
@rendermode InteractiveServer

<NabuAssistant ShowLanguageSelect="true" />
```

`@rendermode InteractiveServer` must be active. You can set it on the page (as above) or directly on the component:

```razor
@page "/"

<NabuAssistant ShowLanguageSelect="true" @rendermode="InteractiveServer" />
```

---

## Razor Pages

### 1. Register services

```csharp
// Program.cs
using Nabu;

builder.Services.AddRazorPages();

builder.Services.AddNabu()
    .AddHandler<MyHandler>();
```

### 2. Add the SiriWave script to `_Layout.cshtml`

```html
<script src="https://cdn.jsdelivr.net/npm/siriwave/dist/siriwave.umd.min.js"></script>
```

Place this before the closing `</body>` tag.

### 3. Add the `@using` and `@addTagHelper` directives to `_ViewImports.cshtml`

```razor
@using Nabu
@addTagHelper *, Nabu.Server
```

### 4. Add `<nabu-assistant>` to a page

```html
<nabu-assistant show-language-select="true"></nabu-assistant>

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

## Voice Commands

Nabu can resolve spoken phrases into discrete command IDs via semantic matching, so you don't need exact keyword lists.

### Register commands in `Program.cs`

```csharp
builder.Services.AddNabu()
    .AddCommandHandler<MyCommandHandler>()
    .AddCommand("increment", ["add one", "count up"], scope: "/counter")
    .AddCommand("reset", ["reset", "start over"], scope: "/counter");
```

### Register commands from an embedded JSON resource

```csharp
builder.Services.AddNabu()
    .AddCommandHandler<MyCommandHandler>()
    .AddCommandsFromResource("commands.json");
```

**Simple format** (no scope):
```json
{
  "increment": ["add one", "count up", "increment"]
}
```

**Extended format** (with scope):
```json
{
  "increment": {
    "scope": "/counter",
    "phrases": ["add one", "count up", "increment"]
  }
}
```

Both formats can be mixed in the same file.

### Implement `INabuCommandHandler`

```csharp
using Nabu;

public class MyCommandHandler : INabuCommandHandler
{
    public Task OnCommandAsync(string commandId, string originalText)
    {
        Console.WriteLine($"Command: {commandId} (from: \"{originalText}\")");
        return Task.CompletedTask;
    }
}
```

### Page-level command dispatch with `VoiceCommandRegistry`

For finer-grained control, inject `VoiceCommandRegistry` into a page and register callbacks directly:

```razor
@inject VoiceCommandRegistry VoiceCommands
@implements IDisposable

@code {
    protected override void OnInitialized()
    {
        VoiceCommands.Register("increment", text => { count++; StateHasChanged(); return Task.CompletedTask; });
        VoiceCommands.Register("reset", text => { count = 0; StateHasChanged(); return Task.CompletedTask; });
    }

    public void Dispose()
    {
        VoiceCommands.Unregister("increment");
        VoiceCommands.Unregister("reset");
    }
}
```

---

## Voice Navigation

Nabu can generate voice navigation phrases automatically from route mappings:

```csharp
builder.Services.AddNabu()
    .AddNavigation(nav => nav
        .Map("/", "home", "homepage")
        .Map("/counter", "counter", "counting page"));
```

This automatically registers phrases like "go to the counter page", "navigate to home", "open the homepage", etc.

---

## Server backend (Nabu.Local)

For server-side inference via Whisper.net, run `Nabu.Local` as a separate service.

### Start

```bash
cd src/Nabu.Local
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

All available variables are listed in `src/Nabu/wwwroot/css/speech-overlay.css`.

---

## Component parameters

### `NabuAssistant`

| Parameter            | Type   | Default | Description                      |
|----------------------|--------|---------|----------------------------------|
| `ShowLanguageSelect` | `bool` | `false` | Show language selection dropdown |

---

## Supported languages

Popular: English, German, French, Spanish, Italian, Portuguese, Chinese, Japanese, Korean, Hindi

~45 additional languages are available via `NabuLanguages.Extended`.

---

## Project structure

```
src/
  Nabu/              # Razor Class Library (main library, NuGet: Nabu)
  Nabu.Server/       # Server-side components: Tag Helper, ViewComponent, command map (NuGet: Nabu.Server)
  Nabu.Core/         # Internal: audio processing, VAD, Whisper, wake-word
  Nabu.Inference/    # Internal: inference interfaces, embedding, command store
  Nabu.Local/        # Optional standalone server service (Whisper.net + SignalR)
examples/
  Nabu.BlazorDemo/        # Demo: Blazor Server
  Nabu.BlazorWasmDemo/    # Demo: Blazor WebAssembly
  Nabu.RazorPagesDemo/    # Demo: Razor Pages
  Nabu.MauiDemo/          # Demo: .NET MAUI
external/
  silero-vad/        # Voice Activity Detection (git submodule, sparse)
  NanoWakeWord/      # Wake-word detection (git submodule)
```
