const BUFFER_SIZE = 2048;

export async function createAudioWorklet(audioContext, onAudioFrame) {
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
        stream = await navigator.mediaDevices.getUserMedia({ audio: true });
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
    const microphoneSource = audioContext.createMediaStreamSource(stream);
    const analyserNode = audioContext.createAnalyser();
    analyserNode.smoothingTimeConstant = 0.8;
    microphoneSource.connect(analyserNode);

    let recorderNode;
    let scriptNode;
    if (audioContext.audioWorklet) {
        await audioContext.audioWorklet.addModule('/_content/Nabu.RCL/audio/audioProcessor.js');
        recorderNode = new AudioWorkletNode(audioContext, 'live-audio-processor');
        recorderNode.port.onmessage = (event) => onAudioFrame(event.data);
        microphoneSource.connect(recorderNode);
    } else {
        scriptNode = audioContext.createScriptProcessor(BUFFER_SIZE, 1, 0);
        scriptNode.onaudioprocess = (event) => {
            const inputData = event.inputBuffer.getChannelData(0);
            onAudioFrame(inputData.slice(0));
        };
        const dummyGain = audioContext.createGain();
        dummyGain.gain.value = 0;
        microphoneSource.connect(scriptNode);
        scriptNode.connect(dummyGain);
        dummyGain.connect(audioContext.destination);
    }

    return {
        stream: microphoneSource.mediaStream,
        analyser: analyserNode,
        sourceNode: microphoneSource,
        workletNode: recorderNode ?? scriptNode,
    };
}
