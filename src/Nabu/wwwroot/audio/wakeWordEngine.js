import {RingBuffer} from '../core/ringBuffer.js';
import {
    SAMPLE_RATE,
    WAKE_WORD_AUDIO_WINDOW_S,
    WAKE_WORD_CHECK_INTERVAL, 
    WAKE_WORD_MIN_AUDIO_S
} from '../core/audioConfig.js';
import {DEFAULT_LANGUAGE, WAKE_WORD_PROMPT, WAKE_WORD_REGEX} from '../core/whisperConfig.js';
import {enqueueInferenceTask, getPooledArrayBuffer, runInferenceInWorker} from '../worker/workerClient.js';
import {timestamp} from '../core/utils.js';

let wakeWordBuffer = new RingBuffer(SAMPLE_RATE * WAKE_WORD_AUDIO_WINDOW_S);
let wakeWordCheckTimer = null;
let wakeWordCheckRunning = false;
let isEnabled = false;

export function pushAudioToWakeWordEngine(audioChunk) {
    if (!isEnabled) return;
    wakeWordBuffer.push(audioChunk);
}

export function startWakeWordEngine(onWakeWordDetected) {
    if (isEnabled) return;
    isEnabled = true;
    scheduleWakeWordCheck(onWakeWordDetected);
}

export function stopWakeWordEngine() {
    isEnabled = false;
    if (wakeWordCheckTimer) {
        clearTimeout(wakeWordCheckTimer);
        wakeWordCheckTimer = null;
    }
    wakeWordBuffer.clear();
}

async function checkForWakeWord(onWakeWordDetected) {
    if (wakeWordCheckRunning || !isEnabled) return;

    if (wakeWordBuffer.length < SAMPLE_RATE * WAKE_WORD_MIN_AUDIO_S) return;
    
    wakeWordCheckRunning = true;

    const requiredSamples = SAMPLE_RATE * WAKE_WORD_AUDIO_WINDOW_S;
    const samplesToRead = Math.min(requiredSamples, wakeWordBuffer.length);

    const audioBuffer = getPooledArrayBuffer(samplesToRead);
    wakeWordBuffer.copyLatestTo(audioBuffer, samplesToRead);

    try {
        await enqueueInferenceTask(async () => {
            if (!isEnabled) return;

            const result = await runInferenceInWorker(audioBuffer, {
                return_timestamps: false,
                initial_prompt: WAKE_WORD_PROMPT,
                language: DEFAULT_LANGUAGE,
            });

            const normalizedText = (result.text ?? '')
                .toLowerCase()
                .replace(/[.,!?;\-]/g, ' ')
                .replace(/\s+/g, ' ')
                .trim();

            const isNoise = !normalizedText
                || /^(\s|\[[^\]]*\]|\{[^}]*\}|\([^)]*\)|\*+|[\[\]()])+$/.test(normalizedText);
            if (!isNoise) {
                console.log(`[${timestamp()}] Wake-word check: "${normalizedText}"`);
            }

            const isWakeWord = WAKE_WORD_REGEX.test(normalizedText);

            if (isWakeWord) {
                wakeWordBuffer.clear();
                if (onWakeWordDetected) onWakeWordDetected();
            }
        });
    } finally {
        wakeWordCheckRunning = false;
    }
}

function scheduleWakeWordCheck(onWakeWordDetected) {
    if (wakeWordCheckTimer || !isEnabled) return;
    wakeWordCheckTimer = setTimeout(() => {
        wakeWordCheckTimer = null;
        checkForWakeWord(onWakeWordDetected).finally(() => {
            if (isEnabled) scheduleWakeWordCheck(onWakeWordDetected);
        });
    }, WAKE_WORD_CHECK_INTERVAL);
}

export function disposeWakeWordEngine() {
    stopWakeWordEngine();
    wakeWordBuffer = new RingBuffer(SAMPLE_RATE * WAKE_WORD_AUDIO_WINDOW_S);
    wakeWordCheckRunning = false;
}
