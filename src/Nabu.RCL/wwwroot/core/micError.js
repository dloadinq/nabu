import {emitStatusMessage} from './appState.js';
import {showToast} from '../ui/toast.js';

const MIC_DENIED_NAMES = ['MIC_DENIED', 'NotAllowedError'];
const MIC_NOT_FOUND_NAMES = ['MIC_NOT_FOUND', 'NotFoundError'];
const INSECURE_CONTEXT_NAMES = ['INSECURE_CONTEXT'];

const MESSAGE_MIC_DENIED = 'Microphone access denied. Please allow microphone in your browser settings.';
const MESSAGE_MIC_NOT_FOUND = 'No microphone found. Please connect a microphone.';

export function isMicError(error) {
    const name = error?.name ?? '';
    return MIC_DENIED_NAMES.includes(name) || MIC_NOT_FOUND_NAMES.includes(name) || INSECURE_CONTEXT_NAMES.includes(name);
}

export function handleMicError(error) {
    const name = error?.name ?? '';
    const isDenied = MIC_DENIED_NAMES.includes(name);
    const isNotFound = MIC_NOT_FOUND_NAMES.includes(name);
    const isInsecureContext = INSECURE_CONTEXT_NAMES.includes(name);

    if (isDenied || isNotFound || isInsecureContext) {
        const msg = isInsecureContext
            ? (error?.message || 'Use localhost or HTTPS for microphone access.')
            : isDenied
                ? MESSAGE_MIC_DENIED
                : MESSAGE_MIC_NOT_FOUND;
        emitStatusMessage(msg);
        showToast(msg, 10000);
    }
    throw error;
}
