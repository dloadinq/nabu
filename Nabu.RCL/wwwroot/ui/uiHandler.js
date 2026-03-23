import { STATES } from '../core/stateMachine.js';
import { WAKE_WORD } from '../core/whisperConfig.js';
import { BACKENDS } from '../core/constants.js';
import { showToast, hasActiveToast } from './toast.js';

const DEFAULT_STATUS = "Listening: Say 'Hey Jarvis' to begin.";

const STATUS_MAP = {
    [STATES.LISTENING]:    { text: DEFAULT_STATUS, style: '' },
    [STATES.KW_DETECTED]:  { text: 'Keyword detected!', style: 'color:#22c55e;font-weight:700' },
    [STATES.TRANSCRIBING]: { text: 'Recording…', style: '' },
    [STATES.FLUSHING]:     { text: 'Processing…', style: '' },
    [STATES.IDLE]:         { text: DEFAULT_STATUS, style: '' },
    [STATES.STOPPED]:      { text: 'Stopped.', style: '' },
};

let lastState = STATES.STOPPED;

export function renderStatus(state) {
    if (state === STATES.LISTENING) {
        if (!hasActiveToast() && (lastState === STATES.IDLE || lastState === STATES.STOPPED)) {
            showToast(DEFAULT_STATUS, 4000);
        }
    } else if (state === STATES.KW_DETECTED) {
        showToast("Keyword detected! Listening for command...", 3000);
    }
    lastState = state;
}

let lastMessage = '';

export function renderStatusMessage(message) {
    const statusElement = document.getElementById('statusInfo');
    if (statusElement) {
        if (message && message !== '') {
            statusElement.textContent = message;
            statusElement.style.display = '';
        } else {
            statusElement.textContent = '';
            statusElement.style.display = 'none';
        }
    }
    if (message && message !== '' && message !== lastMessage) {
        showToast(message, 4000);
        lastMessage = message;
    } else if (!message || message === '') {
        lastMessage = '';
    }
}

export function renderBackendIndicator(backend) {
    const backendInfoElement = document.getElementById('backendInfo');
    if (backendInfoElement) {
        let label;
        if (backend === BACKENDS.SERVICE || backend === 'service') {
            label = 'Server';
        } else if (typeof backend === 'string' && backend.startsWith('browser (')) {
            label = 'Browser' + backend.substring(7);
        } else {
            label = 'Browser';
        }
        backendInfoElement.innerText = `Active backend: ${label}`;
    }
}

export function renderSwitchButton(backend) {
    const switchBtn = document.getElementById('switchBtn');
    if (switchBtn) {
        switchBtn.style.display = backend === BACKENDS.BROWSER ? 'inline-block' : 'none';
    }
}

export function renderLivePreview(text) {
    const outputElement = document.getElementById('output');
    if (outputElement) outputElement.innerText = text;
}

export function renderFinalTranscription(text) {
    const outputElement = document.getElementById('output');
    if (outputElement) {
        outputElement.innerText = text;
        outputElement.scrollTop = outputElement.scrollHeight;
    }
}

export function clearTranscriptionOutput() {
    const outputElement = document.getElementById('output');
    if (outputElement) outputElement.innerText = '';
}
