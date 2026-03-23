/**
 * Application-wide constants: backend identifiers, SignalR hub methods and events,
 * Blazor interop callback names, custom DOM events, storage keys, and mobile detection.
 */
export const BACKENDS = Object.freeze({
    SERVICE: 'service',
    BROWSER: 'browser',
});

export const SERVER_STATUS = Object.freeze({
    LISTENING: 'listening',
    INITIALIZING: 'initializing',
    IDLE: 'idle.',
    READY: 'ready.',
});

export const HUB_METHODS = Object.freeze({
    SEND_AUDIO_CHUNK: 'SendAudioChunk',
    SET_LANGUAGE: 'SetLanguage',
    FINISH_RECORDING: 'FinishRecording',
});

export const HUB_EVENTS = Object.freeze({
    WAKE_WORD_DETECTED: 'OnWakeWordDetected',
    TRANSCRIPTION_PREVIEW: 'OnTranscriptionPreview',
    TRANSCRIPTION_FINAL: 'OnTranscriptionFinal',
    STATUS_CHANGED: 'OnStatusChanged',
});

export const BLAZOR_CALLBACKS = Object.freeze({
    STATE_CHANGED: 'JSCallback_OnStateChanged',
    BACKEND_CHANGED: 'JSCallback_OnBackendChanged',
    TRANSCRIPTION_FINAL: 'JSCallback_OnTranscriptionFinal',
    LIVE_PREVIEW: 'JSCallback_OnLivePreview',
    STATUS_MESSAGE: 'JSCallback_OnStatusMessage',
    UI_STATE_CHANGED: 'JSCallback_OnUIStateChanged',
});

export const CUSTOM_EVENTS = Object.freeze({
    TRANSCRIPTION_FINAL: 'whisper:transcriptionFinal',
    STATE_CHANGED: 'whisper:stateChanged',
    LIVE_PREVIEW: 'whisper:livePreview',
    UI_STATE_CHANGED: 'whisper:uiStateChanged',
    BROWSER_MODEL_LOADING: 'whisper:browserModelLoading',
    BROWSER_MODEL_LOADED: 'whisper:browserModelLoaded',
});

export const SIGNALR_STATE = Object.freeze({
    CONNECTED: 'Connected',
});

export const VISUALIZER_ID = 'visualizer';

export const STORAGE_KEY_WHISPER_LANGUAGE = 'whisper_language';

export const TERMINATION_TEXT = 'Transcription was terminated due to no available voice activity.';
export const NO_RECOGNIZABLE_SPEECH_TEXT = 'No recognizable speech was detected.';

export function isMobileDevice() {
    return /iPhone|iPad|iPod|Android/i.test(navigator.userAgent) || ('ontouchstart' in window && window.innerWidth < 768);
}
