import {STATES} from './core/stateMachine.js';
import {BACKENDS, VISUALIZER_ID, isMobileDevice, CUSTOM_EVENTS, STORAGE_KEY_WHISPER_LANGUAGE} from './core/constants.js';
import {
    appState,
    stateMachine,
    timestamp,
    setActiveBackend,
    emitStateChanged,
    emitBackendChanged,
    emitStatusMessage,
    emitStatusMessageAndFlush,
} from './core/appState.js';
import {handleMicError, isMicError} from './core/micError.js';
import {
    probeServiceBackend,
    ensureServiceConnection,
    disconnectService,
    sendLanguageToServer,
    finishRecording as finishRecordingOnServer,
    handleServiceAudioFrame,
    SERVICE_BASE_URL,
    SERVICE_HUB_URL,
} from './service/serviceClient.js';
import {handleBrowserAudioFrame, onWakeWordDetected} from './browser/browserPipeline.js';
import {startHeartbeat, disposeWebGPU, initBrowserBackend, registerRestartCallback} from './backend/backendManager.js';
import {requestMicrophonePermission, startAudioCapture, stopAudioCapture} from './audio/audioCapture.js';
import {startWakeWordEngine, stopWakeWordEngine} from './audio/wakeWordEngine.js';
import {stopSpeechDetection} from './audio/speechDetector.js';
import {playWakeWordFeedback} from './audio/audioFeedback.js';
import {renderStatus, clearTranscriptionOutput} from './ui/uiHandler.js';
import {stopVisualizer, hideVisualizer} from './ui/audioVisualizer.js';
import {showToast} from './ui/toast.js';
import {initCommandParser} from './core/commandParser.js';
import {setupOverlayManager} from './ui/overlayManager.js';

let isRestarting = false;

export async function initApp(userOptions = {}) {
    appState.transcriptionOptions = {...appState.transcriptionOptions, ...userOptions};

    emitStatusMessage('Click to start.');
    registerRestartCallback(() => startLiveTranscription());
    startHeartbeat();
    setupOverlayManager();
    stateMachine.reset();
    initCommandParser();
}

async function startServiceMode() {
    if (!isRestarting) emitStatusMessage('Connecting to server...');
    appState.currentAnalyserNode = await startAudioCapture(handleServiceAudioFrame);
    await ensureServiceConnection();
    await sendLanguageToServer(appState.transcriptionOptions.language);
    emitStatusMessage('');
    if (!isRestarting) {
        showToast('Connected to server. Say "Hey Jarvis" to begin.', 5000);
    }
    emitBackendChanged(BACKENDS.SERVICE);
    stateMachine.transition(STATES.LISTENING);
    console.log(`[${timestamp()}] Service mode started (${SERVICE_HUB_URL})`);
}

async function startBrowserMode() {
    initBrowserBackend();

    try {
        appState.currentAnalyserNode = await startAudioCapture(handleBrowserAudioFrame);
    } catch (micError) {
        handleMicError(micError);
    }

    if (stateMachine.transition(STATES.LISTENING)) {
        startWakeWordEngine(onWakeWordDetected);
    } else {
        renderStatus(STATES.LISTENING);
    }
    console.log(`[${timestamp()}] Ready (language: ${appState.transcriptionOptions.language})`);
}

export async function startLiveTranscription(userOptions = {}) {
    appState.transcriptionOptions = {...appState.transcriptionOptions, ...userOptions};

    if (!isRestarting) {
        await emitStatusMessageAndFlush('Awaiting permission for microphone input...');
    }
    await new Promise((resolve) => requestAnimationFrame(resolve));
    try {
        await requestMicrophonePermission();
    } catch (micError) {
        handleMicError(micError);
        return;
    }

    if (!isRestarting) emitStatusMessage('Checking server...');
    const serverAvailable = await probeServiceBackend();

    if (serverAvailable) {
        setActiveBackend(BACKENDS.SERVICE);
        try {
            await startServiceMode();
            return;
        } catch (error) {
            if (isMicError(error)) {
                handleMicError(error);
                return;
            }
            if (isMobileDevice()) {
                setActiveBackend(BACKENDS.BROWSER);
                emitStatusMessage('Use a desktop or reconnect to Whisper.Local.');
                showToast('Server connection failed. Browser inference is not supported on mobile.', 6000);
            } else {
                console.warn(`[${timestamp()}] Service backend failed. Falling back to browser.`, error);
                showToast('Server connection failed. Falling back to browser inference.', 5000);
                setActiveBackend(BACKENDS.BROWSER);
                emitStatusMessage('Setting up Browser Inference...');
            }
            await stopAudioCapture();
            stopVisualizer(VISUALIZER_ID);
            if (isMobileDevice()) return;
        }
    } else if (isMobileDevice()) {
        setActiveBackend(BACKENDS.BROWSER);
        emitStatusMessage('Browser inference is not supported on mobile. Use a desktop or connect to Whisper.Local on your network.');
        showToast('Mobile: Use desktop or connect to Whisper.Local server.', 8000);
        return;
    } else {
        setActiveBackend(BACKENDS.BROWSER);
        if (!isRestarting) {
            emitStatusMessage('Setting up Browser Inference...');
            showToast('No server. Loading browser model...', 3000);
            window.dispatchEvent(new CustomEvent(CUSTOM_EVENTS.BROWSER_MODEL_LOADING));
        }
    }

    await startBrowserMode();
}

export async function stopLive() {
    stopVisualizer(VISUALIZER_ID);
    await stopAudioCapture();
    stopWakeWordEngine();
    stopSpeechDetection();
    await disconnectService();

    appState.currentAnalyserNode = null;
    appState.livePreviewRunning = false;
    const transitioned = !stateMachine.is(STATES.STOPPED) && stateMachine.transition(STATES.STOPPED);
    if (!transitioned) {
        emitStateChanged(STATES.STOPPED);
        renderStatus(STATES.STOPPED);
    }

    console.log(`[${timestamp()}] Stopped.`);
}

export async function switchToServer() {
    emitStatusMessage('Checking for server...');
    showToast('Checking for local server...', 2000);

    const wasRunning = !stateMachine.is(STATES.IDLE) && !stateMachine.is(STATES.STOPPED);

    if (wasRunning) {
        await stopLive();
    }

    const serverAvailable = await probeServiceBackend();

    if (!serverAvailable) {
        showToast('Server not available. Staying in browser mode.', 4000);
        emitStatusMessage('Server not found. Browser mode active.');
        return false;
    }

    await disposeWebGPU();
    setActiveBackend(BACKENDS.SERVICE);
    showToast('Switched to server mode! Browser resources freed.', 4000);
    emitStatusMessage('Server connected.');
    console.log(`[${timestamp()}] Switched from browser to server mode.`);
    return true;
}

export function resetTranscript() {
    clearTranscriptionOutput();
}

export function registerDotNetCallback(helper) {
    appState.dotNetHelper = helper;
    console.log(`[${timestamp()}] C# Blazor interop registered.`);
    if (appState.activeBackend) emitBackendChanged(appState.activeBackend);
}

export function startOnUserGesture(optionsOrGetter = {}) {
    return new Promise((resolve) => {
        let started = false;
        const userGestureEvents = ['click', 'keydown', 'touchstart', 'pointerdown'];
        const handler = async (gestureEvent) => {
            if (started) return;
            const languageSelect = document.getElementById('whisperLanguageSelect');
            if (languageSelect && languageSelect.contains?.(gestureEvent?.target)) return;
            started = true;
            userGestureEvents.forEach((eventType) => document.removeEventListener(eventType, handler, true));
            let options = typeof optionsOrGetter === 'function' ? optionsOrGetter() : optionsOrGetter;
            if (languageSelect?.value && languageSelect.value !== '__more__') {
                options = {...options, language: languageSelect.value};
            }
            await startLiveTranscription(options);
            resolve();
        };
        userGestureEvents.forEach((eventType) => document.addEventListener(eventType, handler, {
            capture: true,
            once: false
        }));
    });
}

export async function cancelAndRestart() {
    isRestarting = true;
    try {
        try { await stopLive(); } catch { }
        try { await startLiveTranscription({language: appState.transcriptionOptions.language}); } catch { }
        try { playWakeWordFeedback(); } catch { }
    } finally {
        isRestarting = false;
    }
}

export async function setLanguage(language) {
    appState.transcriptionOptions.language = language;
    await sendLanguageToServer(language);
    console.log(`[${timestamp()}] Language changed to: ${language}`);
}

export function getWhisperSavedLanguage() {
    try {
        return localStorage.getItem(STORAGE_KEY_WHISPER_LANGUAGE) || null;
    } catch {
        return null;
    }
}

export function wireLanguageSelectChange() {
    const languageSelect = document.getElementById('whisperLanguageSelect');
    if (!languageSelect) return;
    languageSelect.addEventListener('change', async (changeEvent) => {
        const selectedValue = changeEvent.target?.value;
        if (!selectedValue || selectedValue === '__more__') return;
        await setLanguage(selectedValue);
        try {
            localStorage.setItem(STORAGE_KEY_WHISPER_LANGUAGE, selectedValue);
        } catch {}
    });
}

export async function finishRecording() {
    if (appState.activeBackend === BACKENDS.SERVICE) {
        await finishRecordingOnServer();
    }
}

export {hideVisualizer};
