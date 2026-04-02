import {createAudioWorklet} from './audioWorklet.js';
import {SAMPLE_RATE, SILENCE_THRESHOLD} from '../core/audioConfig.js';

let audioContext = null;
let microphoneStream = null;
let sourceNode = null;
let workletNode = null;
let analyserNode = null;

export async function requestMicrophonePermission() {
    if (!navigator?.mediaDevices?.getUserMedia) {
        const port = window.location.port || '80';
        const e = new Error(
            `Microphone access requires a secure context (HTTPS or localhost). ` +
            `Please open http://localhost:${port} or http://127.0.0.1:${port} instead of ${window.location.origin}.`
        );
        e.name = 'INSECURE_CONTEXT';
        throw e;
    }
    let stream;
    try {
        stream = await navigator.mediaDevices.getUserMedia({
            audio: {
                echoCancellation: false,
                noiseSuppression: false,
                autoGainControl: false,
                sampleRate: 16000,
                channelCount: 1
            }
        });
    } catch (error) {
        const msg = error?.name === 'NotAllowedError' || error?.message?.includes('Permission')
            ? 'MIC_DENIED'
            : error?.name === 'NotFoundError'
                ? 'MIC_NOT_FOUND'
                : null;
        if (msg) {
            const micError = new Error(error?.message || msg);
            micError.name = msg;
            throw micError;
        }
        throw error;
    }
    stream.getTracks().forEach((t) => t.stop());
}

export async function startAudioCapture(onAudioFrame) {
    if (audioContext) {
        await stopAudioCapture();
    }

    audioContext = new AudioContext({sampleRate: SAMPLE_RATE});
    const result = await createAudioWorklet(audioContext, onAudioFrame);
    microphoneStream = result.stream;
    sourceNode = result.sourceNode;
    workletNode = result.workletNode;
    analyserNode = result.analyser;
    return analyserNode;
}

export async function stopAudioCapture() {
    if (analyserNode) {
        analyserNode.disconnect();
        analyserNode = null;
    }
    if (sourceNode) {
        sourceNode.disconnect();
        sourceNode = null;
    }
    if (workletNode) {
        workletNode.disconnect();
        if (workletNode.port) workletNode.port.close();
        workletNode = null;
    }
    if (microphoneStream) {
        microphoneStream.getTracks().forEach((track) => track.stop());
        microphoneStream = null;
    }
    if (audioContext && audioContext.state !== 'closed') {
        try {
            await audioContext.close();
        } catch (e) {
            console.warn("Couldn't close audioContext gracefully:", e);
        }
        audioContext = null;
    }
}

export function calculateRms(audioChunk) {
    let sumOfSquares = 0;
    for (let i = 0; i < audioChunk.length; i++) {
        sumOfSquares += audioChunk[i] * audioChunk[i];
    }
    return Math.sqrt(sumOfSquares / audioChunk.length);
}

export function isSilence(rms) {
    return rms < SILENCE_THRESHOLD;
}
