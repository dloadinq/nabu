/**
 * Whisper model configuration: wake word phrase, transcription prompt,
 * available model sizes (base/tiny), and inference device types (WebGPU/WASM).
 */
export const WAKE_WORD = 'hey jarvis';
export const WAKE_WORD_PROMPT = 'Hey Jarvis.';

export const DEFAULT_TRANSCRIPTION_PROMPT =
    'Hey Jarvis. Transcribe the following speech into correct, natural sentences ' +
    'with proper grammar and punctuation. Do not add anything that was not said. ' +
    'Preserve domain-specific terms, names, and technical vocabulary exactly.';

export const DEFAULT_LANGUAGE = 'english';

export const WHISPER_MODELS = Object.freeze({
    BASE: 'whisper-base',
    TINY: 'whisper-tiny',
    BASE_PATH: 'Xenova/whisper-base',
    TINY_PATH: 'Xenova/whisper-tiny',
});

export const DEVICES = Object.freeze({
    WEBGPU: 'webgpu',
    WASM: 'wasm',
});
