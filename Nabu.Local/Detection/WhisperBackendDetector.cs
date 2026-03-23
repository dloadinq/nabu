using System.Text.RegularExpressions;
using Whisper.net.Logger;

namespace Nabu.Local.Detection;

public class WhisperBackendDetector
{
    private string? _detectedBackend;

    private static readonly Dictionary<string, string> RuntimeLabels = new()
    {
        ["cuda"] = "CUDA (NVIDIA GPU)",
        ["vulkan"] = "Vulkan (GPU)",
        ["coreml"] = "CoreML / Metal (Apple)",
        ["openvino"] = "OpenVINO (Intel)",
        ["noavx"] = "CPU (no AVX)",
        ["cpu"] = "CPU"
    };

    /// <summary>
    /// Whether the detected backend is a GPU backend (CUDA, Vulkan, CoreML).
    /// Only valid after Whisper initialization.
    /// </summary>
    public bool IsGpuBackend => _detectedBackend is "cuda" or "vulkan" or "coreml";

    public void AttachToWhisperLogs()
    {
        LogProvider.AddLogger((_, msg) =>
        {
            if (_detectedBackend != null || string.IsNullOrEmpty(msg)) return;

            var match = Regex.Match(msg, @"runtimes[/\\]([a-z0-9]+)[/\\]", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                _detectedBackend = match.Groups[1].Value.ToLowerInvariant() switch
                {
                    "cuda" => "cuda",
                    "vulkan" => "vulkan",
                    "coreml" => "coreml",
                    "openvino" => "openvino",
                    "noavx" => "noavx",
                    "cpu" or "win-x64" or "linux-x64" => "cpu",
                    _ => match.Groups[1].Value.ToLowerInvariant()
                };
            }
            else if (msg.Contains("ggml_cuda_init") || msg.Contains("CUDA0 total size"))
            {
                _detectedBackend = "cuda";
            }
            else if (msg.Contains("ggml_vulkan") || (msg.Contains("Successfully loaded") && msg.Contains("vulkan")))
            {
                _detectedBackend = "vulkan";
            }
        });
    }

    public string GetDisplayLabel()
    {
        if (_detectedBackend != null && RuntimeLabels.TryGetValue(_detectedBackend, out var label))
            return label;
        return _detectedBackend ?? "CPU (Standard)";
    }
}
