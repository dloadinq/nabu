import {STATES} from '../core/stateMachine.js';
import {
    HUB_METHODS,
    HUB_EVENTS,
    SERVER_STATUS,
    SIGNALR_STATE,
    VISUALIZER_ID,
} from '../core/constants.js';
import {
    appState,
    stateMachine,
    timestamp,
    emitStateChanged,
    emitTranscriptionFinal,
    emitLivePreview,
    emitStatusMessage,
} from '../core/appState.js';
import {renderStatus, clearTranscriptionOutput} from '../ui/uiHandler.js';
import {startVisualizer, stopVisualizer} from '../ui/audioVisualizer.js';
import {playWakeWordFeedback} from '../audio/audioFeedback.js';

export const SERVICE_BASE_URL = 'http://127.0.0.1:50000';
export const SERVICE_HUB_URL = `${SERVICE_BASE_URL}/voiceHub`;

const SERVICE_PROBE_URL = `${SERVICE_BASE_URL}/blast`;
const SERVICE_CHUNK_SAMPLES = 1024;

let serviceConnection = null;
let serviceSignalR = null;
let serviceSendQueue = Promise.resolve();
let serviceSampleQueue = [];

async function loadSignalR() {
    if (serviceSignalR) return serviceSignalR;

    const candidateUrls = [
        'https://cdn.jsdelivr.net/npm/@microsoft/signalr@8.0.7/dist/browser/signalr.min.js',
        'https://unpkg.com/@microsoft/signalr@8.0.7/dist/browser/signalr.min.js',
    ];

    let signalRLoaded = false;
    if (globalThis.signalR?.HubConnectionBuilder) {
        signalRLoaded = true;
    } else {
        for (const url of candidateUrls) {
            try {
                await loadScriptOnce(url);
                if (globalThis.signalR?.HubConnectionBuilder) {
                    signalRLoaded = true;
                    break;
                }
            } catch (error) {
                console.warn(`[${timestamp()}] Failed to load SignalR client from ${url}`);
            }
        }
    }

    if (!signalRLoaded) throw new Error('Unable to load SignalR browser client.');

    serviceSignalR = globalThis.signalR;
    return serviceSignalR;
}

function loadScriptOnce(url) {
    return new Promise((resolve, reject) => {
        const existing = document.querySelector(`script[data-signalr-url="${url}"]`);
        if (existing) {
            if (existing.dataset.loaded === 'true') {
                resolve();
                return;
            }
            existing.addEventListener('load', () => resolve(), {once: true});
            existing.addEventListener('error', () => reject(new Error(`Script failed: ${url}`)), {once: true});
            return;
        }

        const script = document.createElement('script');
        script.src = url;
        script.async = true;
        script.dataset.signalrUrl = url;
        script.addEventListener(
            'load',
            () => {
                script.dataset.loaded = 'true';
                resolve();
            },
            {once: true}
        );
        script.addEventListener('error', () => reject(new Error(`Script failed: ${url}`)), {once: true});
        document.head.appendChild(script);
    });
}

export async function probeServiceBackend() {
    try {
        const controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), 2000);
        const response = await fetch(SERVICE_PROBE_URL, {
            method: 'GET',
            cache: 'no-store',
            signal: controller.signal,
        });
        clearTimeout(timeout);
        return response.ok;
    } catch {
        return false;
    }
}

function uint8ArrayToBase64(bytes) {
    let binary = '';
    for (let i = 0; i < bytes.length; i += 8192) {
        const chunk = bytes.subarray(i, Math.min(i + 8192, bytes.length));
        binary += String.fromCharCode.apply(null, chunk);
    }
    return btoa(binary);
}

function queueServiceChunk(samples) {
    if (!serviceConnection || !samples.length) return;
    const rawBytes = new Uint8Array(samples.length * 2);
    for (let sampleIndex = 0; sampleIndex < samples.length; sampleIndex++) {
        const pcmSample = samples[sampleIndex];
        rawBytes[sampleIndex * 2] = pcmSample & 0xff;
        rawBytes[sampleIndex * 2 + 1] = (pcmSample >> 8) & 0xff;
    }
    const base64 = uint8ArrayToBase64(rawBytes);

    serviceSendQueue = serviceSendQueue
        .then(() => serviceConnection.invoke(HUB_METHODS.SEND_AUDIO_CHUNK, base64))
        .catch((sendError) => console.warn(`[${timestamp()}] Service send failed:`, sendError));
}

function flushServiceSamples(force = false) {
    while (serviceSampleQueue.length >= SERVICE_CHUNK_SAMPLES || (force && serviceSampleQueue.length > 0)) {
        const chunkSize = force ? serviceSampleQueue.length : SERVICE_CHUNK_SAMPLES;
        const chunk = serviceSampleQueue.splice(0, chunkSize);
        queueServiceChunk(chunk);
    }
}

export function handleServiceAudioFrame(audioChunk) {
    if (!serviceConnection) return;
    const len = audioChunk.length;
    for (let i = 0; i < len; i++) {
        const clampedSample = audioChunk[i] < -1 ? -1 : audioChunk[i] > 1 ? 1 : audioChunk[i];
        serviceSampleQueue.push(clampedSample < 0 ? ((clampedSample * 32768 + 0.5) | 0) : ((clampedSample * 32767 + 0.5) | 0));
    }
    flushServiceSamples();
}

export async function ensureServiceConnection() {
    if (serviceConnection) {
        return serviceConnection;
    }

    const signalR = await loadSignalR();
    const connection = new signalR.HubConnectionBuilder()
        .withUrl(SERVICE_HUB_URL)
        .withAutomaticReconnect()
        .build();

    connection.on(HUB_EVENTS.WAKE_WORD_DETECTED, () => {
        if (!stateMachine.is(STATES.LISTENING)) return;
        if (!stateMachine.transition(STATES.KW_DETECTED, STATES.LISTENING)) return;
        clearTranscriptionOutput();
        playWakeWordFeedback();
        if (appState.currentAnalyserNode) startVisualizer(VISUALIZER_ID, appState.currentAnalyserNode);
    });

    connection.on(HUB_EVENTS.TRANSCRIPTION_PREVIEW, (text) => {
        emitLivePreview(text);
    });

    connection.on(HUB_EVENTS.TRANSCRIPTION_FINAL, (text) => {
        emitTranscriptionFinal(text);
        stopVisualizer(VISUALIZER_ID);
        const canTransition = stateMachine.is(STATES.FLUSHING) || stateMachine.is(STATES.TRANSCRIBING);
        if (!canTransition || !stateMachine.transition(STATES.LISTENING, stateMachine.current)) {
            renderStatus(STATES.LISTENING);
            emitStateChanged(STATES.LISTENING);
        }
    });

    connection.on(HUB_EVENTS.STATUS_CHANGED, (status) => {
        const statusLower = status.toLowerCase();
        if (statusLower.includes(SERVER_STATUS.LISTENING)) {
            const transitioned = stateMachine.is(STATES.KW_DETECTED) &&
                stateMachine.transition(STATES.TRANSCRIBING, STATES.KW_DETECTED);
            if (!transitioned) {
                renderStatus(STATES.TRANSCRIBING);
                emitStateChanged(STATES.TRANSCRIBING);
            }
        } else if (statusLower.includes(SERVER_STATUS.INITIALIZING)) {
            const transitioned = stateMachine.is(STATES.TRANSCRIBING) &&
                stateMachine.transition(STATES.FLUSHING, STATES.TRANSCRIBING);
            if (!transitioned) {
                renderStatus(STATES.FLUSHING);
                emitStateChanged(STATES.FLUSHING);
            }
        } else if (statusLower === SERVER_STATUS.IDLE || statusLower === SERVER_STATUS.READY) {
            stopVisualizer(VISUALIZER_ID);
            const canTransition = stateMachine.is(STATES.FLUSHING) || stateMachine.is(STATES.TRANSCRIBING) || stateMachine.is(STATES.KW_DETECTED);
            const transitioned = canTransition && stateMachine.transition(STATES.LISTENING, stateMachine.current);
            if (!transitioned) {
                renderStatus(STATES.LISTENING);
                emitStateChanged(STATES.LISTENING);
            }
        } else {
            emitStatusMessage(status);
        }
    });

    await connection.start();
    serviceConnection = connection;
    console.log(`[${timestamp()}] Connected to Whisper.Local: ${SERVICE_HUB_URL}`);
    return connection;
}

export async function disconnectService() {
    flushServiceSamples(true);
    serviceSampleQueue = [];

    if (serviceConnection) {
        try {
            await serviceConnection.stop();
        } catch {
        }
        serviceConnection = null;
        console.log(`[${timestamp()}] Disconnected from Whisper.Local.`);
    }
}

export async function sendLanguageToServer(language) {
    if (serviceConnection && serviceConnection.state === SIGNALR_STATE.CONNECTED) {
        try {
            await serviceConnection.invoke(HUB_METHODS.SET_LANGUAGE, language);
            console.log(`[${timestamp()}] Language set on server: ${language}`);
        } catch (error) {
            console.warn(`[${timestamp()}] Failed to set language on server:`, error);
        }
    }
}

export async function finishRecording() {
    if (serviceConnection && serviceConnection.state === SIGNALR_STATE.CONNECTED) {
        try {
            await serviceConnection.invoke(HUB_METHODS.FINISH_RECORDING);
            console.log(`[${timestamp()}] FinishRecording sent to server`);
        } catch (error) {
            console.warn(`[${timestamp()}] Failed to finish recording on server:`, error);
        }
    }
}
