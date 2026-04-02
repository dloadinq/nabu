export const WAKE_WORD_PROMPT = 'Hey Jarvis.';
export const WAKE_WORD_REGEX = /hey\s*(jarvis|jervis|travis|java)/i;
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
