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
    CANCEL_RECORDING: 'CancelRecording',
    GET_COMMAND_HASHES: 'GetCommandHashes',
    PATCH_COMMANDS: 'PatchCommands',
    SET_PAGE_CONTEXT: 'SetPageContext',
});

export const HUB_EVENTS = Object.freeze({
    WAKE_WORD_DETECTED: 'OnWakeWordDetected',
    TRANSCRIPTION_PREVIEW: 'OnTranscriptionPreview',
    TRANSCRIPTION_FINAL: 'OnTranscriptionFinal',
    STATUS_CHANGED: 'OnStatusChanged',
    COMMAND_RESOLVED: 'OnCommandResolved'
});

export const BLAZOR_CALLBACKS = Object.freeze({
    STATE_CHANGED: 'JSCallback_OnStateChanged',
    BACKEND_CHANGED: 'JSCallback_OnBackendChanged',
    TRANSCRIPTION_FINAL: 'JSCallback_OnTranscriptionFinal',
    LIVE_PREVIEW: 'JSCallback_OnLivePreview',
    STATUS_MESSAGE: 'JSCallback_OnStatusMessage',
    UI_STATE_CHANGED: 'JSCallback_OnUIStateChanged',
    COMMAND_RESOLVED: 'JSCallback_OnCommandResolved',
});

export const CUSTOM_EVENTS = Object.freeze({
    TRANSCRIPTION_FINAL: 'nabu:transcriptionFinal',
    STATE_CHANGED: 'nabu:stateChanged',
    LIVE_PREVIEW: 'nabu:livePreview',
    UI_STATE_CHANGED: 'nabu:uiStateChanged',
    BROWSER_MODEL_LOADING: 'nabu:browserModelLoading',
    BROWSER_MODEL_LOADED: 'nabu:browserModelLoaded',
});

export const SIGNALR_STATE = Object.freeze({
    CONNECTED: 'Connected',
});

export const VISUALIZER_ID = 'visualizer';
export const STORAGE_KEY_LANGUAGE = 'nabu_language';
export const EXPAND_LANGUAGES_KEY = '__more__';
export const COLLAPSE_LANGUAGES_KEY = '__less__';
export const OVERLAY_STATUS_DONE = 'Done.';
export const OVERLAY_STATUS_KEYWORD_DETECTED = 'Keyword detected!';
export const TERMINATION_TEXT = 'Transcription was terminated due to no available voice activity.';
export const NO_RECOGNIZABLE_SPEECH_TEXT = 'No recognizable speech was detected.';
export const USER_CANCELLED_TEXT = 'Transcription was cancelled due to user action.';

export function isMobileDevice() {
    return /iPhone|iPad|iPod|Android/i.test(navigator.userAgent) || ('ontouchstart' in window && window.innerWidth < 768);
}
