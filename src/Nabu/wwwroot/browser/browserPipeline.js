import {STATES} from '../core/stateMachine.js';
import {WAKE_WORD_ACK_DELAY_MS} from '../core/audioConfig.js';
import {VISUALIZER_ID} from '../core/constants.js';
import {appState, emitLivePreview, emitTranscriptionFinal, stateMachine, timestamp,} from '../core/appState.js';
import {enqueueInferenceTask, getPooledArrayBuffer, runInferenceInWorker,} from '../worker/workerClient.js';
import {pushAudioToWakeWordEngine, startWakeWordEngine} from '../audio/wakeWordEngine.js';
import {copySpeechBufferTo, pushAudioToSpeechDetector, startSpeechDetection,} from '../audio/speechDetector.js';
import {startVisualizer, stopVisualizer} from '../ui/audioVisualizer.js';
import {playWakeWordFeedback} from '../audio/audioFeedback.js';

function buildInferenceOptions() {
    return {
        return_timestamps: false,
        chunk_length_s: 30,
        stride_length_s: 5,
        language: appState.transcriptionOptions.language,
        initial_prompt: appState.transcriptionOptions.initialPrompt,
    };
}

async function flushSpeechToText(samplesToProcess) {
    if (samplesToProcess <= 0) return;

    const audioBuffer = getPooledArrayBuffer(samplesToProcess);
    copySpeechBufferTo(audioBuffer, samplesToProcess);

    await enqueueInferenceTask(async () => {
        const result = await runInferenceInWorker(audioBuffer, buildInferenceOptions());

        const transcribedSentence = result.text?.trim() ?? '';

        if (transcribedSentence.length > 0) {
            emitTranscriptionFinal(transcribedSentence);
        }
    });

    console.log(`[${timestamp()}] Transcription done -> LISTENING`);
    if (stateMachine.transitionFrom(STATES.FLUSHING, STATES.LISTENING)) {
        stopVisualizer(VISUALIZER_ID);
        startWakeWordEngine(onWakeWordDetected);
    }
}

async function updateLivePreview(samplesToProcess) {
    if (samplesToProcess <= 0) return;
    appState.livePreviewRunning = true;

    const audioBuffer = getPooledArrayBuffer(samplesToProcess);
    copySpeechBufferTo(audioBuffer, samplesToProcess);

    await enqueueInferenceTask(async () => {
        const result = await runInferenceInWorker(audioBuffer, buildInferenceOptions());

        const previewText = result.text?.trim() ?? '';
        if (previewText.length > 0) {
            emitLivePreview(previewText, true);
        }
    });

    appState.livePreviewRunning = false;
}

export function onWakeWordDetected() {
    appState.livePreviewLength = 0;
    if (!stateMachine.transitionFrom(STATES.LISTENING, STATES.KW_DETECTED)) return;

    playWakeWordFeedback();
    if (appState.currentAnalyserNode) startVisualizer(VISUALIZER_ID, appState.currentAnalyserNode);

    setTimeout(() => {
        startSpeechDetection();
    }, WAKE_WORD_ACK_DELAY_MS);
}

export function handleBrowserAudioFrame(audioChunk) {
    if (stateMachine.is(STATES.LISTENING)) {
        pushAudioToWakeWordEngine(audioChunk);
        return;
    }

    if (stateMachine.is(STATES.TRANSCRIBING) || stateMachine.is(STATES.KW_DETECTED)) {
        pushAudioToSpeechDetector(
            audioChunk,
            (samplesToProcess) => {
                if (stateMachine.transitionFrom(stateMachine.current, STATES.FLUSHING)) {
                    flushSpeechToText(samplesToProcess);
                }
            },
            (samplesToProcess) => {
                if (!appState.livePreviewRunning) {
                    updateLivePreview(samplesToProcess);
                }
            },
            () => {
                if (stateMachine.is(STATES.KW_DETECTED)) {
                    stateMachine.transitionFrom(STATES.KW_DETECTED, STATES.TRANSCRIBING);
                }
            }
        );
    }
}
