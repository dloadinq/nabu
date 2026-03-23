import {StateMachine} from './stateMachine.js';
import {DEFAULT_LANGUAGE} from './whisperConfig.js';
import {BLAZOR_CALLBACKS, CUSTOM_EVENTS} from './constants.js';
import {
    renderStatus,
    renderStatusMessage,
    renderLivePreview,
    renderFinalTranscription,
    renderBackendIndicator,
    renderSwitchButton,
} from '../ui/uiHandler.js';
import {showToast} from '../ui/toast.js';

export const timestamp = () => new Date().toISOString().substring(11, 23);

export const appState = {
    transcriptionOptions: {
        language: DEFAULT_LANGUAGE,
        initialPrompt: '',
        onTranscriptionFinal: null,
    },
    dotNetHelper: null,
    activeBackend: null,
    currentAnalyserNode: null,
    livePreviewRunning: false,
    livePreviewLength: 0,
};

export function emitStateChanged(state) {
    window.dispatchEvent(new CustomEvent(CUSTOM_EVENTS.STATE_CHANGED, {detail: state}));
    if (appState.dotNetHelper) {
        appState.dotNetHelper.invokeMethodAsync(BLAZOR_CALLBACKS.STATE_CHANGED, state);
    }
}

export const stateMachine = new StateMachine((state) => {
    renderStatus(state);
    emitStateChanged(state);
});

export function emitBackendChanged(backend) {
    renderBackendIndicator(backend);
    renderSwitchButton(backend);
    if (appState.dotNetHelper) {
        appState.dotNetHelper.invokeMethodAsync(BLAZOR_CALLBACKS.BACKEND_CHANGED, backend);
    }
}

export function setActiveBackend(backend) {
    appState.activeBackend = backend;
    emitBackendChanged(backend);
}

function filterBracketedContent(text) {
    if (!text || typeof text !== 'string')
        return '';
    const regex = /\s*(\[[^\]]*\]|\([^)]*\)|<[^>]*>|\*+[^*]+\*+)\s*/g;
    return text
        .replace(regex, ' ')
        .replace(/\s+/g, ' ')
        .trim();
}

export function emitTranscriptionFinal(text) {
    const filtered = filterBracketedContent(text);
    appState.livePreviewLength = 0;
    renderFinalTranscription(filtered);
    if (appState.dotNetHelper) {
        appState.dotNetHelper.invokeMethodAsync(BLAZOR_CALLBACKS.TRANSCRIPTION_FINAL, filtered);
    }
    if (typeof appState.transcriptionOptions.onTranscriptionFinal === 'function') {
        appState.transcriptionOptions.onTranscriptionFinal(filtered);
    }
    window.dispatchEvent(new CustomEvent(CUSTOM_EVENTS.TRANSCRIPTION_FINAL, {detail: filtered}));
}

export function emitLivePreview(text, onlyIfLonger = false) {
    if (onlyIfLonger && text.length < appState.livePreviewLength) return;
    if (text.length > 0) appState.livePreviewLength = text.length;
    renderLivePreview(text);
    window.dispatchEvent(new CustomEvent(CUSTOM_EVENTS.LIVE_PREVIEW, {detail: text}));
    if (appState.dotNetHelper) {
        appState.dotNetHelper.invokeMethodAsync(BLAZOR_CALLBACKS.LIVE_PREVIEW, text);
    }
}

export function emitStatusMessage(message) {
    renderStatusMessage(message);
    if (appState.dotNetHelper) {
        appState.dotNetHelper.invokeMethodAsync(BLAZOR_CALLBACKS.STATUS_MESSAGE, message);
    }
}

export async function emitStatusMessageAndFlush(message) {
    emitStatusMessage(message);
    await new Promise((resolve) => setTimeout(resolve, 0));
}

export function emitUIStateChanged(uiState) {
    if (appState.dotNetHelper) {
        appState.dotNetHelper.invokeMethodAsync(BLAZOR_CALLBACKS.UI_STATE_CHANGED, uiState).catch(() => {});
    }
    if (typeof window !== 'undefined') {
        window.dispatchEvent(new CustomEvent(CUSTOM_EVENTS.UI_STATE_CHANGED, { detail: uiState }));
    }
}
