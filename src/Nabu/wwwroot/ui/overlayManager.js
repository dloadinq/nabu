import {STATES} from '../core/stateMachine.js';
import {appState, emitTranscriptionFinal, emitUIStateChanged} from '../core/appState.js';
import {restartLive, stopLive, stopLiveKeepConnection} from '../app.js';
import {CUSTOM_EVENTS, NO_RECOGNIZABLE_SPEECH_TEXT, TERMINATION_TEXT, USER_CANCELLED_TEXT} from '../core/constants.js';
import {showToast} from "./toast.js";

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
let _userCancelFired = false;

let uiState = {
    showOverlay: false,
    overlayStatus: '',
    overlayText: '',
    isPreviewText: true,
    showCancelButton: false,
    isCancelling: false,
    isProcessing: false
};

function dispatchUIState() {
    emitUIStateChanged({...uiState});
}

function updateState(updates) {
    uiState = {...uiState, ...updates};
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

        appState.suppressTranscription = true;

        updateState({
            isCancelling: true,
            overlayStatus: 'Cancelling...',
            overlayText: 'No speech was detected for 5s.',
            isPreviewText: false,
            showOverlay: true,
            showCancelButton: false
        });
        
        try {
            await stopLiveKeepConnection();
        } catch (error) {
            console.log("Error stopping live audio on no speech timeout", error);
        }

        clearDismissTimer();
        _dismissTimer = setTimeout(async () => {
            _noSpeechFired = false;
            updateState({
                isCancelling: false,
                showOverlay: false,
                overlayText: '',
                overlayStatus: '',
                showCancelButton: false,
                isProcessing: false
            });
            const msg = TERMINATION_TEXT;
            showToast(msg, 4000);
            emitTranscriptionFinal(msg, true);
            
            appState.suppressTranscription = false;
            try {
                await restartLive();
            } catch {}
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
    document.addEventListener('click', (e) => {
        if (e.target.closest('.speech-dialog__cancel-btn')) {
            cancelByUser();
        }
    });

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
                showCancelButton: false
            });

            _openDelayTimer = setTimeout(() => {
                updateState({showOverlay: true});
            }, OverlayOpenDelayMs);

            startNoSpeechTimer();

        } else if (state === STATES.TRANSCRIBING) {
            clearNoSpeechTimer();
            const updates = {
                overlayStatus: 'Recording...',
                showCancelButton: true,
                isProcessing: true
            };
            if (!uiState.showOverlay) {
                clearOpenDelayTimer();
                updates.showOverlay = true;
            }
            updateState(updates);

        } else if (state === STATES.FLUSHING) {
            updateState({
                overlayStatus: 'Processing...',
                showCancelButton: false,
                isProcessing: true
            });
        } else if (state === STATES.STOPPED) {
            clearNoSpeechTimer();
            clearOpenDelayTimer();
            if (!uiState.isCancelling) {
                clearDismissTimer();
                updateState({
                    showOverlay: false,
                    showCancelButton: false,
                    isProcessing: false,
                    isCancelling: false,
                    overlayText: '',
                    overlayStatus: '',
                });
            }
        } else if (state === STATES.LISTENING || state === STATES.IDLE) {
            updateState({
                showCancelButton: false,
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
            overlayStatus: 'Recording...'
        });
    });

    window.addEventListener(CUSTOM_EVENTS.TRANSCRIPTION_FINAL, (finalEvent) => {
        if (_noSpeechFired || _userCancelFired || uiState.isCancelling || appState.suppressTranscription) return;
        clearNoSpeechTimer();

        const text = finalEvent.detail || '';

        if (isOnlyNonSpeechSounds(text)) {
            const msg = NO_RECOGNIZABLE_SPEECH_TEXT;
            updateState({
                isCancelling: true,
                overlayStatus: 'Cancelling...',
                overlayText: msg,
                isPreviewText: false,
                showOverlay: true,
                showCancelButton: false
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
            }, NoRecognizableSpeechDismissMs);
            return;
        }

        updateState({
            overlayText: text,
            isPreviewText: false,
            overlayStatus: 'Done.',
            showCancelButton: false
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

export async function cancelByUser() {
    clearNoSpeechTimer();
    clearOpenDelayTimer();
    clearDismissTimer();

    _noSpeechFired = false;
    _userCancelFired = false;


    if (appState.dotNetHelper) {
        appState.dotNetHelper.invokeMethodAsync('JSCallback_SetManualCancelBlock', true);
    }

    appState.suppressTranscription = true;

    updateState({
        isCancelling: true,
        overlayStatus: 'Cancelling...',
        overlayText: 'User cancelled the transcription.',
        isPreviewText: false,
        showOverlay: true,
        showCancelButton: false
    });

    try {
        await stopLiveKeepConnection();
    } catch (err) {
        console.error("Error stopping live audio", err);
    }

    _dismissTimer = setTimeout(async () => {
        _dismissTimer = null;

        updateState({
            isCancelling: false,
            showOverlay: false,
            overlayText: '',
            overlayStatus: '',
            showCancelButton: false,
            isProcessing: false
        });

        _userCancelFired = true;
        showToast(USER_CANCELLED_TEXT, 4000);

        emitTranscriptionFinal(USER_CANCELLED_TEXT, true);

        if (appState.dotNetHelper) {
            appState.dotNetHelper.invokeMethodAsync('JSCallback_SetManualCancelBlock', false);
        }
        _userCancelFired = false;
        appState.suppressTranscription = false;

        try {
            await restartLive();
        } catch {
        }
    }, NoSpeechCancelDismissMs);
}