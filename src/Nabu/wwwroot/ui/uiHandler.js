import {STATES} from '../core/stateMachine.js';
import {BACKENDS} from '../core/constants.js';
import {hasActiveToast, showToast} from './toast.js';

const DEFAULT_STATUS = "Listening: Say 'Hey Jarvis' to begin.";

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
    if (!backendInfoElement) return;

    let label = 'Browser';

    if (backend === BACKENDS.SERVICE || backend === 'service') {
        label = 'Server';
    } else if (typeof backend === 'string' && backend.startsWith('browser (')) {
        label = backend.replace('browser', 'Browser');
    }

    backendInfoElement.textContent = `Active backend: ${label}`;
}