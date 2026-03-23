import { RingBuffer } from '../core/ringBuffer.js';
import { calculateRms, isSilence } from './audioCapture.js';
import {
    SAMPLE_RATE,
    SILENCE_DURATION_MS,
    MIN_SPEECH_DURATION_S,
    LIVE_PREVIEW_TRIGGER_S,
} from '../core/audioConfig.js';

let speechBuffer = new RingBuffer(SAMPLE_RATE * 30);
let silenceTimer = null;
let userIsSpeaking = false;
let isEnabled = false;
let lastPreviewTimestamp = 0;

export function startSpeechDetection() {
    isEnabled = true;
    userIsSpeaking = false;
    speechBuffer.clear();
}

export function stopSpeechDetection() {
    isEnabled = false;
    userIsSpeaking = false;
    speechBuffer.clear();

    if (silenceTimer) {
        clearTimeout(silenceTimer);
        silenceTimer = null;
    }
}

export function pushAudioToSpeechDetector(audioChunk, onSpeechEnded, onLivePreviewRequested, onSpeechStarted) {
    if (!isEnabled) return;

    speechBuffer.push(audioChunk);
    const rms = calculateRms(audioChunk);

    if (isSilence(rms)) {
        const minSamplesBeforeFlush = SAMPLE_RATE * MIN_SPEECH_DURATION_S;

        if (!silenceTimer && userIsSpeaking && speechBuffer.length > minSamplesBeforeFlush) {
            userIsSpeaking = false;

            silenceTimer = setTimeout(() => {
                silenceTimer = null;
                if (!isEnabled) return;

                const samplesToRead = speechBuffer.length;
                onSpeechEnded(samplesToRead);
                speechBuffer.clear();
            }, SILENCE_DURATION_MS);
        }
    } else {
        if (silenceTimer) {
            clearTimeout(silenceTimer);
            silenceTimer = null;
        }

        if (!userIsSpeaking) {
            userIsSpeaking = true;
            if (onSpeechStarted) onSpeechStarted();
        }

        const enoughAudioForPreview = speechBuffer.length >= SAMPLE_RATE * LIVE_PREVIEW_TRIGGER_S;
        const now = Date.now();

        if (enoughAudioForPreview && now - lastPreviewTimestamp > 800) {
            lastPreviewTimestamp = now;
            onLivePreviewRequested(speechBuffer.length);
        }
    }
}

export function copySpeechBufferTo(targetArray, samplesToRead) {
    return speechBuffer.copyLatestTo(targetArray, samplesToRead);
}

export function disposeSpeechDetector() {
    stopSpeechDetection();
    speechBuffer = new RingBuffer(SAMPLE_RATE * 30);
    lastPreviewTimestamp = 0;
}
