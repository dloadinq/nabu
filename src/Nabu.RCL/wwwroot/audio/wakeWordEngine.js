import { RingBuffer } from '../core/ringBuffer.js';
import {
    SAMPLE_RATE,
    WAKE_WORD_CHECK_INTERVAL,
    WAKE_WORD_AUDIO_WINDOW_S,
} from '../core/audioConfig.js';
import { WAKE_WORD, WAKE_WORD_PROMPT, DEFAULT_LANGUAGE } from '../core/whisperConfig.js';
import { runInferenceInWorker, enqueueInferenceTask, getPooledArrayBuffer } from '../worker/workerClient.js';

let wakeWordBuffer = new RingBuffer(SAMPLE_RATE * WAKE_WORD_AUDIO_WINDOW_S);
let wakeWordCheckTimer = null;
let wakeWordCheckRunning = false;
let isEnabled = false;

const timestamp = () => new Date().toISOString().substring(11, 23);

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

    const requiredSamples = SAMPLE_RATE * WAKE_WORD_AUDIO_WINDOW_S;
    if (wakeWordBuffer.length < SAMPLE_RATE * 0.5) return;

    wakeWordCheckRunning = true;

    const samplesToRead = Math.min(requiredSamples, wakeWordBuffer.length);
    const audioBuffer = getPooledArrayBuffer(samplesToRead);
    wakeWordBuffer.copyLatestTo(audioBuffer, samplesToRead);

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
            || /^[\[\]()*\s]+$/.test(normalizedText)
            || /^(\s|\[[^\]]*\]|\{[^}]*\}|\([^)]*\)|\*+|[\[\]()])+$/.test(normalizedText);
        if (!isNoise) {
            console.log(`[${timestamp()}] Wake-word check: "${normalizedText}"`);
        }

        const isWakeWord = /hey\s*(jarvis|jervis|travis|java)/i.test(normalizedText);

        if (isWakeWord) {
            console.log(`[${timestamp()}] Wake-word detected!`);
            wakeWordBuffer.clear();

            if (onWakeWordDetected) {
                onWakeWordDetected();
            }
        }
    });

    wakeWordCheckRunning = false;
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
