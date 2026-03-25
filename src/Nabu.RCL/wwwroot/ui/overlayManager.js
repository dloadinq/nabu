import { STATES } from '../core/stateMachine.js';
import { CUSTOM_EVENTS, TERMINATION_TEXT, NO_RECOGNIZABLE_SPEECH_TEXT } from '../core/constants.js';
import { emitUIStateChanged, emitTranscriptionFinal } from '../core/appState.js';
import { cancelAndRestart, stopLive, hideVisualizer } from '../app.js';
import { showToast } from './toast.js';

const OverlayOpenDelayMs = 1000;
const NoSpeechTimeoutMs = 5000;
const NoSpeechCancelDismissMs = 2000;
const NoRecognizableSpeechDismissMs = 2000;
const FinalTextDismissMs = 2000;

const NON_SPEECH_REGEX = /^[\s*\[\]\(\)]+$/;

let _dismissTimer = null;
let _openDelayTimer = null;
let _noSpeechTimer = null;
let _noSpeechFired = false;

let uiState = {
    showOverlay: false,
    overlayStatus: '',
    overlayText: '',
    isPreviewText: true,
    showSendButton: false,
    isCancelling: false,
    isProcessing: false
};

function dispatchUIState() {
    emitUIStateChanged({ ...uiState });
}

function updateState(updates) {
    uiState = { ...uiState, ...updates };
    dispatchUIState();
}

function clearNoSpeechTimer() {
    if (_noSpeechTimer) {
        clearTimeout(_noSpeechTimer);
        _noSpeechTimer = null;
    }
}

function clearDismissTimer() {
    if (_dismissTimer) {
        clearTimeout(_dismissTimer);
        _dismissTimer = null;
    }
}

function clearOpenDelayTimer() {
    if (_openDelayTimer) {
        clearTimeout(_openDelayTimer);
        _openDelayTimer = null;
    }
}

function startNoSpeechTimer() {
    clearNoSpeechTimer();
    _noSpeechFired = false;

    _noSpeechTimer = setTimeout(async () => {
        _noSpeechTimer = null;
        _noSpeechFired = true;

        updateState({
            isCancelling: true,
            overlayStatus: 'Cancelling…',
            overlayText: 'No speech was detected for 3s.',
            isPreviewText: false,
            showOverlay: true,
            showSendButton: false
        });

        try { await stopLive(); } catch { }
        try { hideVisualizer(); } catch { }

        clearDismissTimer();
        _dismissTimer = setTimeout(async () => {
            _noSpeechFired = false;
            updateState({
                isCancelling: false,
                showOverlay: false,
                overlayText: '',
                overlayStatus: '',
                showSendButton: false,
                isProcessing: false
            });
            const msg = TERMINATION_TEXT;
            showToast(msg, 4000);
            emitTranscriptionFinal(msg);
            try { await cancelAndRestart(); } catch { }
        }, NoSpeechCancelDismissMs);

    }, NoSpeechTimeoutMs);
}

function isOnlyNonSpeechSounds(text) {
    if (!text || typeof text !== 'string') return true;
    const trimmed = text.trim();
    if (trimmed.length === 0) return true;
    return NON_SPEECH_REGEX.test(trimmed);
}

export function setupOverlayManager() {
    window.addEventListener(CUSTOM_EVENTS.STATE_CHANGED, (stateEvent) => {
        const state = stateEvent.detail;

        if (state === STATES.KW_DETECTED) {
            clearDismissTimer();
            clearOpenDelayTimer();
            _noSpeechFired = false;

            updateState({
                overlayStatus: 'Keyword detected!',
                overlayText: '',
                isPreviewText: true,
                isCancelling: false,
                isProcessing: true,
                showSendButton: false
            });

            _openDelayTimer = setTimeout(() => {
                updateState({ showOverlay: true });
            }, OverlayOpenDelayMs);

            startNoSpeechTimer();

        } else if (state === STATES.TRANSCRIBING) {
            clearNoSpeechTimer();
            const updates = {
                overlayStatus: 'Recording…',
                showSendButton: true,
                isProcessing: true
            };
            if (!uiState.showOverlay) {
                clearOpenDelayTimer();
                updates.showOverlay = true;
            }
            updateState(updates);

        } else if (state === STATES.FLUSHING) {
            updateState({
                overlayStatus: 'Processing…',
                showSendButton: true,
                isProcessing: true
            });

        } else if (state === STATES.LISTENING || state === STATES.IDLE || state === STATES.STOPPED) {
            updateState({
                showSendButton: false,
                isProcessing: false
            });
        }
    });

    window.addEventListener(CUSTOM_EVENTS.LIVE_PREVIEW, (previewEvent) => {
        if (uiState.isCancelling || _noSpeechFired) return;
        clearNoSpeechTimer();

        updateState({
            overlayText: previewEvent.detail,
            isPreviewText: true,
            overlayStatus: 'Recording…'
        });
    });

    window.addEventListener(CUSTOM_EVENTS.TRANSCRIPTION_FINAL, (finalEvent) => {
        if (_noSpeechFired) return;
        clearNoSpeechTimer();

        const text = finalEvent.detail || '';

        if (isOnlyNonSpeechSounds(text)) {
            const msg = NO_RECOGNIZABLE_SPEECH_TEXT;
            updateState({
                isCancelling: true,
                overlayStatus: 'Cancelling…',
                overlayText: msg,
                isPreviewText: false,
                showOverlay: true,
                showSendButton: false
            });

            emitTranscriptionFinal(msg);

            clearDismissTimer();
            _dismissTimer = setTimeout(async () => {
                updateState({
                    isCancelling: false,
                    showOverlay: false,
                    overlayText: '',
                    overlayStatus: '',
                    isProcessing: false
                });
                showToast(msg, 4000);
                try { await cancelAndRestart(); } catch { }
            }, NoRecognizableSpeechDismissMs);
            return;
        }

        updateState({
            overlayText: text,
            isPreviewText: false,
            overlayStatus: 'Done.',
            showSendButton: false
        });

        clearDismissTimer();
        _dismissTimer = setTimeout(() => {
            updateState({
                showOverlay: false,
                overlayText: '',
                overlayStatus: ''
            });
        }, FinalTextDismissMs);
    });
}
