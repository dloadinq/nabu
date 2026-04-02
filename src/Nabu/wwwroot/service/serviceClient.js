import {STATES} from '../core/stateMachine.js';
import {HUB_EVENTS, HUB_METHODS, SERVER_STATUS, SIGNALR_STATE, VISUALIZER_ID,} from '../core/constants.js';
import {
    appState, 
    emitCommandResolved,
    emitLivePreview,
    emitStateChanged,
    emitStatusMessage,
    emitTranscriptionFinal,
    stateMachine,
    timestamp,
} from '../core/appState.js';
import {renderStatus} from '../ui/uiHandler.js';
import {startVisualizer, stopVisualizer} from '../ui/audioVisualizer.js';
import {playWakeWordFeedback} from '../audio/audioFeedback.js';

export const SERVICE_BASE_URL = 'http://127.0.0.1:50000';
export const SERVICE_HUB_URL = `${SERVICE_BASE_URL}/voiceHub`;

const SERVICE_PROBE_URL = `${SERVICE_BASE_URL}/blast`;
const SERVICE_CHUNK_SAMPLES = 1024;

let serviceConnection = null;
let serviceSignalR = null;
let serviceSendQueue = Promise.resolve();
let pendingCommandSyncItems = null;
let pendingPageRoute = null;

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

function queueServiceChunk(samples) {
    if (!serviceConnection || !samples.length) return;

    const pcmBuffer = new Int16Array(samples.length);
    for (let i = 0; i < samples.length; i++) {
        const s = Math.max(-1, Math.min(1, samples[i]));
        pcmBuffer[i] = s < 0 ? s * 0x8000 : s * 0x7FFF;
    }

    const bytes = new Uint8Array(pcmBuffer.buffer);
    let binary = '';
    const len = bytes.byteLength;
    for (let i = 0; i < len; i++) {
        binary += String.fromCharCode(bytes[i]);
    }
    const base64 = btoa(binary);

    serviceSendQueue = serviceSendQueue
        .then(async () => {
            if (serviceConnection.state === "Connected") {
                await serviceConnection.invoke('SendAudioChunk', base64);
            }
        })
        .catch((err) => {
            console.warn('Send failed (Service disconnected):', err);
            serviceSendQueue = Promise.resolve();
        });
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
    for (let i = 0; i < audioChunk.length; i++) {
        serviceSampleQueue.push(audioChunk[i]);
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

    connection.onclose(err => {
        if (serviceConnection === connection) serviceConnection = null;
        if (err) console.warn(`[${timestamp()}] Service connection closed:`, err.message ?? err);
    });
    
    connection.on(HUB_EVENTS.WAKE_WORD_DETECTED, () => {
        if (!stateMachine.is(STATES.LISTENING)) return;
        if (!stateMachine.transitionFrom(STATES.LISTENING, STATES.KW_DETECTED)) return;
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
        if (!canTransition || !stateMachine.transitionFrom(stateMachine.current, STATES.LISTENING)) {
            renderStatus(STATES.LISTENING);
            emitStateChanged(STATES.LISTENING);
        }
    });

    connection.on(HUB_EVENTS.STATUS_CHANGED, (status) => {
        const statusLower = status.toLowerCase();
        if (statusLower.includes(SERVER_STATUS.LISTENING)) {
            const transitioned = stateMachine.is(STATES.KW_DETECTED) &&
                stateMachine.transitionFrom(STATES.KW_DETECTED, STATES.TRANSCRIBING);
            if (!transitioned) {
                renderStatus(STATES.TRANSCRIBING);
                emitStateChanged(STATES.TRANSCRIBING);
            }
        } else if (statusLower.includes(SERVER_STATUS.INITIALIZING)) {
            const transitioned = stateMachine.is(STATES.TRANSCRIBING) &&
                stateMachine.transitionFrom(STATES.TRANSCRIBING, STATES.FLUSHING);
            if (!transitioned) {
                renderStatus(STATES.FLUSHING);
                emitStateChanged(STATES.FLUSHING);
            }
        } else if (statusLower === SERVER_STATUS.IDLE || statusLower === SERVER_STATUS.READY) {
            stopVisualizer(VISUALIZER_ID);
            const canTransition = stateMachine.is(STATES.FLUSHING) || stateMachine.is(STATES.TRANSCRIBING) || stateMachine.is(STATES.KW_DETECTED);
            const transitioned = canTransition && stateMachine.transitionFrom(stateMachine.current, STATES.LISTENING);
            if (!transitioned) {
                renderStatus(STATES.LISTENING);
                emitStateChanged(STATES.LISTENING);
            }
        } else {
            emitStatusMessage(status);
        }
    });

    connection.on(HUB_EVENTS.COMMAND_RESOLVED, (commandId, text) => {
        emitCommandResolved(commandId, text);
    });

    await connection.start();
    serviceConnection = connection;

    if (pendingCommandSyncItems) {
        try {
            await performCommandDeltaSync(connection, pendingCommandSyncItems);
        } catch (err) {
            console.warn(`[${timestamp()}] Command sync failed:`, err);
        }
    }

    if (pendingPageRoute) {
        try {
            await connection.invoke(HUB_METHODS.SET_PAGE_CONTEXT, pendingPageRoute);
        } catch (err) {
            console.warn(`[${timestamp()}] Page context sync failed:`, err);
        }
    }
    
    return connection;
}

export async function setPageContext(route) {
    pendingPageRoute = route;
    if (serviceConnection?.state === SIGNALR_STATE.CONNECTED) {
        try {
            await serviceConnection.invoke(HUB_METHODS.SET_PAGE_CONTEXT, route);
        } catch (error) {
            console.warn(`[${timestamp()}] Failed to set page context:`, error);
        }
    }
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
        } catch {
        }
    }
}

export function setCommandSyncItems(items) {
    pendingCommandSyncItems = items;
    if (serviceConnection?.state === SIGNALR_STATE.CONNECTED) {
        performCommandDeltaSync(serviceConnection, items)
            .catch(err => console.warn(`[${timestamp()}] Command sync failed:`, err));
    }
}

export async function finishRecording() {
    if (!serviceConnection || serviceConnection.state !== SIGNALR_STATE.CONNECTED) return;

    flushServiceSamples(true);

    await (serviceSendQueue = serviceSendQueue.then(async () => {
        try {
            await serviceConnection.invoke(HUB_METHODS.FINISH_RECORDING);
            console.log(`[${timestamp()}] FinishRecording sent to server`);
        } catch (error) {
            console.warn(`[${timestamp()}] Failed to finish recording on server:`, error);
        }
    }));
}

export async function cancelRecording() {
    if (!serviceConnection || serviceConnection.state !== SIGNALR_STATE.CONNECTED) return;
    try {
        await serviceConnection.invoke(HUB_METHODS.CANCEL_RECORDING);
        console.log(`[${timestamp()}] CancelRecording sent to server`);
    } catch (error) {
        console.warn(`[${timestamp()}] Failed to cancel recording on server:`, error);
    }
}

async function performCommandDeltaSync(connection, items) {
    const serverHashesArray = await connection.invoke(HUB_METHODS.GET_COMMAND_HASHES);
    const serverHashes = new Set(serverHashesArray);

    const upserts = [];
    const retainedHashes = items.map(item => item.hash);

    for (const item of items) {
        if (!serverHashes.has(item.hash)) {
            upserts.push(item);
        }
    }

    if (upserts.length > 0 || serverHashesArray.length !== retainedHashes.length) {
        await connection.invoke(HUB_METHODS.PATCH_COMMANDS, upserts, retainedHashes);
        console.log(`[${timestamp()}] Synced Delta: ${upserts.length} upserts, retaining ${retainedHashes.length} items on server.`);
    } else {
        console.log(`[${timestamp()}] Server commands are up to date. No delta sync needed.`);
    }
}