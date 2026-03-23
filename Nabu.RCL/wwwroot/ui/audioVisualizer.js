import {VISUALIZER_ID} from '../core/constants.js';

const VOICE_BIN_COUNT = 40;
const VOLUME_EXPONENT = 1.2;
const VOLUME_SCALE = 6.0;
const MIN_AMPLITUDE = 0.1;
const MAX_AMPLITUDE = 4.0;

let animationId = null;
let isVisualizing = false;
let siriWave = null;
let resizeObserver = null;

export function startVisualizer(containerId, analyserNode) {
    if (isVisualizing) return;
    isVisualizing = true;

    const container = document.getElementById(containerId);
    if (!container) return;

    container.style.opacity = '1';
    container.style.maxHeight = '300px';
    container.style.overflow = 'visible';

    if (!globalThis.SiriWave) {
        console.warn('[Whisper] SiriWave not loaded. Add <script src="https://cdn.jsdelivr.net/npm/siriwave/dist/siriwave.umd.min.js"></script> before the app code.');
        isVisualizing = false;
        return;
    }

    if (siriWave) {
        siriWave.start();
    }

    analyserNode.fftSize = 512;
    const bufferLength = analyserNode.frequencyBinCount;
    const dataArray = new Uint8Array(bufferLength);

    function draw() {
        if (!isVisualizing) return;
        
        if (!siriWave) {
            if (container.offsetWidth >= 10) {
                const waveWidth = container.offsetWidth;
                const waveHeight = 280;
                
                siriWave = new globalThis.SiriWave({
                    container: container,
                    width: waveWidth,
                    height: waveHeight,
                    style: 'ios9',
                    speed: 0.1,
                    amplitude: 0,
                    autostart: true,
                });
            } else {
                animationId = requestAnimationFrame(draw);
                return;
            }
        }

        animationId = requestAnimationFrame(draw);
        analyserNode.getByteFrequencyData(dataArray);

        let sum = 0;
        for (let i = 0; i < VOICE_BIN_COUNT; i++) {
            sum += dataArray[i];
        }

        const averageVolume = sum / VOICE_BIN_COUNT;
        let normalizedVolume = averageVolume / 255.0;

        normalizedVolume = Math.pow(normalizedVolume, VOLUME_EXPONENT) * VOLUME_SCALE;
        normalizedVolume = Math.max(MIN_AMPLITUDE, Math.min(MAX_AMPLITUDE, normalizedVolume));

        siriWave.setAmplitude(normalizedVolume);
    }

    draw();
}

export function stopVisualizer(containerId) {
    isVisualizing = false;

    if (animationId) {
        cancelAnimationFrame(animationId);
        animationId = null;
    }

    if (resizeObserver) {
        resizeObserver.disconnect();
        resizeObserver = null;
    }

    if (siriWave) {
        siriWave.stop();
        siriWave.dispose?.();
        siriWave = null;
    }

    const container = document.getElementById(containerId);
    if (container) {
        container.style.opacity = '0';
        container.style.maxHeight = '0';
        container.style.overflow = 'hidden';
        container.innerHTML = '';
    }
}

export function disposeVisualizer(containerId) {
    stopVisualizer(containerId);
    if (siriWave) {
        siriWave.dispose?.();
        siriWave = null;
    }
    const container = document.getElementById(containerId);
    if (container) container.innerHTML = '';
}

export function hideVisualizer(containerId = VISUALIZER_ID) {
    const container = document.getElementById(containerId);
    if (container) {
        container.style.setProperty('opacity', '0');
        container.style.setProperty('max-height', '0');
    }
}
