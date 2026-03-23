import { pipeline } from 'https://cdn.jsdelivr.net/npm/@huggingface/transformers@3.0.0';
import { WORKER_MSG } from './workerMessages.js';
import { WHISPER_MODELS, DEVICES } from '../core/whisperConfig.js';

let whisperPipeline = null;

self.onmessage = async (e) => {
    const { type, payload, id } = e.data;

    if (type === WORKER_MSG.DISPOSE) {
        if (whisperPipeline) {
            try { await whisperPipeline.dispose?.(); } catch {}
            whisperPipeline = null;
        }
        return;
    }

    if (type === WORKER_MSG.LOAD_MODEL) {
        if (whisperPipeline) {
            self.postMessage({ type: WORKER_MSG.MODEL_LOADED, device: DEVICES.WEBGPU, model: WHISPER_MODELS.BASE });
            return;
        }

        const isFirefox = navigator.userAgent.toLowerCase().includes('firefox');

        if (!isFirefox) {
            try {
                whisperPipeline = await pipeline('automatic-speech-recognition', WHISPER_MODELS.BASE_PATH, {
                    device: DEVICES.WEBGPU,
                });
                self.postMessage({ type: WORKER_MSG.MODEL_LOADED, device: DEVICES.WEBGPU, model: WHISPER_MODELS.BASE });
                return;
            } catch (error) {
                console.warn('WebGPU initialization failed, falling back to WASM:', error);
            }
        } else {
            console.warn('Firefox detected: WebGPU initialization is skipped to prevent hangs. Falling back to WASM.');
        }

        try {
            whisperPipeline = await pipeline('automatic-speech-recognition', WHISPER_MODELS.TINY_PATH, {
                device: DEVICES.WASM,
            });
            self.postMessage({ type: WORKER_MSG.MODEL_LOADED, device: DEVICES.WASM, model: WHISPER_MODELS.TINY });
        } catch (error) {
            console.error('WASM fallback also failed:', error);
        }
    } else if (type === WORKER_MSG.INFERENCE) {
        if (!whisperPipeline) {
            self.postMessage({ type: WORKER_MSG.INFERENCE_ERROR, id, error: 'Model not loaded' });
            return;
        }

        const { audio, options } = payload;
        try {
            const result = await whisperPipeline(audio, options);

            self.postMessage(
                {
                    type: WORKER_MSG.INFERENCE_RESULT,
                    id,
                    result,
                    reclaimedBuffer: audio.buffer,
                },
                [audio.buffer]
            );
        } catch (error) {
            self.postMessage(
                {
                    type: WORKER_MSG.INFERENCE_ERROR,
                    id,
                    error: error.message,
                    reclaimedBuffer: audio.buffer,
                },
                [audio.buffer]
            );
        }
    }
};
