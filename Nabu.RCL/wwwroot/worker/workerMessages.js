/**
 * Message type constants for communication between the main thread and the Whisper Web Worker.
 */
export const WORKER_MSG = Object.freeze({
    LOAD_MODEL: 'LOAD_MODEL',
    MODEL_LOADED: 'MODEL_LOADED',
    INFERENCE: 'INFERENCE',
    INFERENCE_RESULT: 'INFERENCE_RESULT',
    INFERENCE_ERROR: 'INFERENCE_ERROR',
    DISPOSE: 'DISPOSE',
});
