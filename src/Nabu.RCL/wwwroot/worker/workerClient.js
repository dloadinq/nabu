import { WORKER_MSG } from './workerMessages.js';

const timestamp = () => new Date().toISOString().substring(11, 23);

let worker = null;
let inferenceIdCounter = 0;
const pendingInferences = new Map();

const arrayBufferPool = [];

export function getPooledArrayBuffer(size) {
    if (arrayBufferPool.length > 0) {
        for (let i = 0; i < arrayBufferPool.length; i++) {
            if (arrayBufferPool[i].byteLength >= size * 4) {
                const buffer = arrayBufferPool.splice(i, 1)[0];
                return new Float32Array(buffer, 0, size);
            }
        }
    }

    return new Float32Array(size);
}

function returnBufferToPool(buffer) {
    if (buffer && buffer.byteLength > 0 && arrayBufferPool.length < 5) {
        arrayBufferPool.push(buffer);
    }
}

export function initWorkerClient(onModelLoaded) {
    if (worker) return;
    worker = new Worker('/_content/Nabu.RCL/worker/whisperWorker.js', { type: 'module' });
    worker.onmessage = (e) => {
        const { type, device, model, id, result, error, reclaimedBuffer } = e.data;

        if (reclaimedBuffer) {
            returnBufferToPool(reclaimedBuffer);
        }

        if (type === WORKER_MSG.MODEL_LOADED) {
            if (onModelLoaded) onModelLoaded(device, model);
        } else if (type === WORKER_MSG.INFERENCE_RESULT) {
            if (pendingInferences.has(id)) {
                pendingInferences.get(id).resolve(result);
                pendingInferences.delete(id);
            }
        } else if (type === WORKER_MSG.INFERENCE_ERROR) {
            if (pendingInferences.has(id)) {
                pendingInferences.get(id).reject(new Error(error));
                pendingInferences.delete(id);
            }
        }
    };
}

export function loadModelInWorker() {
    if (worker) {
        worker.postMessage({ type: WORKER_MSG.LOAD_MODEL });
    }
}

export function runInferenceInWorker(audioBuffer, options) {
    return new Promise((resolve, reject) => {
        const id = ++inferenceIdCounter;
        pendingInferences.set(id, { resolve, reject });

        worker.postMessage(
            {
                type: WORKER_MSG.INFERENCE,
                id,
                payload: { audio: audioBuffer, options },
            },
            [audioBuffer.buffer]
        );
    });
}

let inferenceQueue = Promise.resolve();
export function enqueueInferenceTask(inferenceTask) {
    inferenceQueue = inferenceQueue
        .then(() => inferenceTask())
        .catch((error) => console.error(`[${timestamp()}] Inference failed:`, error));
    return inferenceQueue;
}

export function isWorkerInitialized() {
    return worker !== null;
}

export function disposeWorkerClient() {
    if (worker) {
        worker.postMessage({ type: WORKER_MSG.DISPOSE });
        worker.terminate();
        worker = null;
        console.log(`[${timestamp()}] Worker terminated.`);
    }
    for (const [id, { reject }] of pendingInferences) {
        reject(new Error('Worker disposed'));
    }
    pendingInferences.clear();
    arrayBufferPool.length = 0;
    inferenceIdCounter = 0;
    inferenceQueue = Promise.resolve();
}
