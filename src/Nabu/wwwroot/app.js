import {STATES} from './core/stateMachine.js';
import {
    BACKENDS,
    CUSTOM_EVENTS,
    isMobileDevice,
    STORAGE_KEY_LANGUAGE,
    EXPAND_LANGUAGES_KEY,
    COLLAPSE_LANGUAGES_KEY,
    VISUALIZER_ID
} from './core/constants.js';
import {
    appState,
    emitBackendChanged,
    emitStateChanged,
    emitStatusMessage,
    emitStatusMessageAndFlush,
    setActiveBackend,
    stateMachine,
    timestamp,
} from './core/appState.js';
import {handleMicError, isMicError} from './core/micError.js';
import {
    disconnectService,
    ensureServiceConnection,
    handleServiceAudioFrame,
    probeServiceBackend,
    sendLanguageToServer,
    setCommandSyncItems as setCommandSyncItemsInService,
    finishRecording as finishRecordingOnServer,
    cancelRecording as cancelRecordingOnServer,
    setPageContext as setPageContextOnServer
} from './service/serviceClient.js';
import {handleBrowserAudioFrame, onWakeWordDetected} from './browser/browserPipeline.js';
import {initBrowserBackend, registerRestartCallback, startHeartbeat} from './backend/backendManager.js';
import {requestMicrophonePermission, startAudioCapture, stopAudioCapture} from './audio/audioCapture.js';
import {startWakeWordEngine, stopWakeWordEngine} from './audio/wakeWordEngine.js';
import {stopSpeechDetection} from './audio/speechDetector.js';
import {renderStatus} from './ui/uiHandler.js';
import {stopVisualizer} from './ui/audioVisualizer.js';
import {showToast} from './ui/toast.js';
import {initCommandParser} from './core/commandParser.js';
import {setupOverlayManager} from './ui/overlayManager.js';

let isRestarting = false;

export async function initApp(userOptions = {}) {
    appState.transcriptionOptions = {...appState.transcriptionOptions, ...userOptions};

    emitStatusMessage('Click to start.');
    registerRestartCallback(() => restartLive());
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
}

export async function startLiveTranscription(userOptions = {}) {
    appState.transcriptionOptions = {...appState.transcriptionOptions, ...userOptions};
    appState.suppressTranscription = false;
    
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
            if (isMicError(error)) return handleMicError(error);

            console.warn(`[${timestamp()}] Service failed to start. Falling back...`, error);
            await stopAudioCapture();
            stopVisualizer(VISUALIZER_ID);
        }
    } 
    
    setActiveBackend(BACKENDS.BROWSER);

    if (isMobileDevice()) {
        const msg = serverAvailable
            ? 'Server connection failed. Browser inference not supported on mobile.'
            : 'Browser inference not supported on mobile. Please connect to Whisper.Local.';

        emitStatusMessage(msg);
        showToast(msg, 8000);
        return;
    }
    
    if (!isRestarting) {
        emitStatusMessage('Setting up Browser Inference...');
        showToast('No server available. Loading browser model...', 3000);
        window.dispatchEvent(new CustomEvent(CUSTOM_EVENTS.BROWSER_MODEL_LOADING));
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

export function registerDotNetCallback(helper) {
    appState.dotNetHelper = helper;
    console.log(`[${timestamp()}] C# Blazor interop registered.`);
    if (appState.activeBackend) emitBackendChanged(appState.activeBackend);
}

export function unregisterDotNetCallback() {
    appState.dotNetHelper = null;
}

export function startOnUserGesture(language = 'english') {
    return new Promise((resolve) => {
        let started = false;
        const userGestureEvents = ['click', 'keydown', 'touchstart', 'pointerdown'];
        const handler = async (gestureEvent) => {
            if (started) return;
            const languageSelect = document.getElementById('nabuLanguageSelect');
            if (languageSelect && languageSelect.contains?.(gestureEvent?.target)) return;
            started = true;
            userGestureEvents.forEach((eventType) => document.removeEventListener(eventType, handler, true));
            await startLiveTranscription({language});+-
            resolve();
        };
        userGestureEvents.forEach((eventType) => document.addEventListener(eventType, handler, {
            capture: true,
            once: false
        }));
    });
}

export async function setLanguage(language) {
    appState.transcriptionOptions.language = language;
    try {
        localStorage.setItem(STORAGE_KEY_LANGUAGE, language);
    } catch {
    }
    await sendLanguageToServer(language);
}

export function getSavedLanguage() {
    try {
        return localStorage.getItem(STORAGE_KEY_LANGUAGE) || null;
    } catch {
        return null;
    }
}

export function addLanguageSelectEvent() {
    const languageSelect = document.getElementById('nabuLanguageSelect');
    if (!languageSelect) return;

    languageSelect.removeEventListener('change', handleLanguageChange);
    languageSelect.addEventListener('change', handleLanguageChange);
}

async function handleLanguageChange(event) {
    const selectedValue = event.target.value;
    if (!selectedValue) return;

    await appState.dotNetHelper.invokeMethodAsync('JSCallback_OnLanguageChangeRequested', selectedValue);

    if (selectedValue !== EXPAND_LANGUAGES_KEY) {
        await setLanguage(selectedValue);
    }
}

export async function stopLiveKeepConnection() {
    stopVisualizer(VISUALIZER_ID);
    await stopAudioCapture();
    stopWakeWordEngine();
    stopSpeechDetection();

    if (appState.activeBackend === BACKENDS.SERVICE) {
        await cancelRecordingOnServer();
    }

    appState.currentAnalyserNode = null;
    appState.livePreviewRunning = false;
    const transitioned = !stateMachine.is(STATES.STOPPED) && stateMachine.transition(STATES.STOPPED);
    if (!transitioned) {
        emitStateChanged(STATES.STOPPED);
        renderStatus(STATES.STOPPED);
    }
}

export async function restartLive() {
    isRestarting = true;
    try {
        await startLiveTranscription({language: appState.transcriptionOptions.language});
    } finally {
        isRestarting = false;
    }
}

export function setCommandSyncItems(items) {
    setCommandSyncItemsInService(items);
}

export async function setPageContext(route) {
    await setPageContextOnServer(route);
}

export async function finishRecording() {
    if (appState.activeBackend === BACKENDS.SERVICE) {
        stateMachine.transition(STATES.FLUSHING, STATES.TRANSCRIBING);
        await finishRecordingOnServer();
    } else {
        forceBrowserFinalize();
    }
}
