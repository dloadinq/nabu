import {STATES} from '../core/stateMachine.js';
import {BACKENDS, VISUALIZER_ID} from '../core/constants.js';
import {
    appState,
    emitBackendChanged,
    emitStatusMessage,
    setActiveBackend,
    stateMachine,
    timestamp
} from '../core/appState.js';
import {disconnectService, probeServiceBackend} from '../service/serviceClient.js';
import {stopAudioCapture} from '../audio/audioCapture.js';
import {disposeWakeWordEngine, stopWakeWordEngine} from '../audio/wakeWordEngine.js';
import {disposeSpeechDetector, stopSpeechDetection} from '../audio/speechDetector.js';
import {disposeVisualizer, stopVisualizer} from '../ui/audioVisualizer.js';
import {disposeWorkerClient, initWorkerClient, loadModelInWorker} from '../worker/workerClient.js';
import {WHISPER_MODELS} from '../core/whisperConfig.js';
import {showToast} from '../ui/toast.js';

const TOAST_TINY_MODEL = 'Warning: WebGPU unavailable. Falling back to smaller model (whisper-tiny). Quality may be reduced.';

let hasLoadedModelOnce = false;

export function initBrowserBackend() {
    initWorkerClient((device, model) => {
        emitBackendChanged(`browser (${device === 'webgpu' ? 'WebGPU' : 'WASM'})`);
        emitStatusMessage('');

        if (!hasLoadedModelOnce) {
            hasLoadedModelOnce = true;
            if (model === WHISPER_MODELS.TINY) {
                showToast(TOAST_TINY_MODEL, 7000);
            } else {
                showToast('Successfully loaded browser model. Say "Hey Jarvis" to begin.', 5000);
            }
        }
    });
    loadModelInWorker();
}

let heartbeatIntervalId = null;
let heartbeatBusy = false;
const HEARTBEAT_INTERVAL_SERVER = 3000;
const HEARTBEAT_INTERVAL_BROWSER = 5000;

let _onRestartNeeded = null;

export function registerRestartCallback(cb) {
    _onRestartNeeded = cb;
}

export function startHeartbeat() {
    if (heartbeatIntervalId) return;
    scheduleNextHeartbeat();
}

function scheduleNextHeartbeat() {
    const interval = appState.activeBackend === BACKENDS.SERVICE
        ? HEARTBEAT_INTERVAL_SERVER
        : HEARTBEAT_INTERVAL_BROWSER;

    heartbeatIntervalId = setTimeout(async () => {
        if (heartbeatBusy) {
            scheduleNextHeartbeat();
            return;
        }
        heartbeatBusy = true;
        try {
            const alive = await probeServiceBackend();

            if (!alive && appState.activeBackend === BACKENDS.SERVICE) {
                showToast('Server went offline. Switching to browser mode...', 5000);
                await fallbackToBrowser();
            } else if (alive && appState.activeBackend === BACKENDS.BROWSER) {
                showToast('Server detected! Switching to server mode...', 4000);
                await autoSwitchToServer();
            }
        } finally {
            heartbeatBusy = false;
            scheduleNextHeartbeat();
        }
    }, interval);
}

export async function fallbackToBrowser() {
    stopVisualizer(VISUALIZER_ID);
    await stopAudioCapture();
    await disconnectService();

    setActiveBackend(BACKENDS.BROWSER);
    emitStatusMessage('Server offline. Setting up Browser Inference...');

    stateMachine.reset();

    if (_onRestartNeeded) {
        try {
            await _onRestartNeeded();
        } catch (error) {
            console.error(`[${timestamp()}] Failed to restart after fallback:`, error);
        }
    }
}

export async function autoSwitchToServer() {
    const wasRunning = !stateMachine.is(STATES.IDLE) && !stateMachine.is(STATES.STOPPED);
    if (wasRunning) {
        stopVisualizer(VISUALIZER_ID);
        await stopAudioCapture();
        stopWakeWordEngine();
        stopSpeechDetection();
    }

    await disposeWebGPU();
    setActiveBackend(BACKENDS.SERVICE);
    emitStatusMessage('Server detected. Establishing connection...');
    stateMachine.reset();
    console.log(`[${timestamp()}] Auto-switched from browser to server mode.`);

    if (_onRestartNeeded) {
        try {
            await _onRestartNeeded();
        } catch (error) {
            console.error(`[${timestamp()}] Failed to restart after server switch:`, error);
        }
    }
}

export async function disposeWebGPU() {
    console.log(`[${timestamp()}] Disposing all WebGPU/browser resources...`);
    stopWakeWordEngine();
    stopSpeechDetection();
    await stopAudioCapture();
    disposeVisualizer(VISUALIZER_ID);
    disposeWakeWordEngine();
    disposeSpeechDetector();
    disposeWorkerClient();
    hasLoadedModelOnce = false;
    appState.livePreviewRunning = false;
    console.log(`[${timestamp()}] WebGPU resources disposed. RAM freed.`);
}
