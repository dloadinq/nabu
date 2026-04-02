class AudioProcessor extends AudioWorkletProcessor {
    constructor() {
        super();
        this.bufferSize = 1024;
        this.buffer = new Float32Array(this.bufferSize);
        this.bufferIndex = 0;
    }

    process(inputs) {
        const firstChannel = inputs[0];
        if (!firstChannel || firstChannel.length === 0) return true;

        const inputData = firstChannel[0];
        if (!inputData) return true;

        for (let i = 0; i < inputData.length; i++) {
            this.buffer[this.bufferIndex++] = inputData[i];

            if (this.bufferIndex >= this.bufferSize) {
                this.port.postMessage(this.buffer.slice(0));
                this.bufferIndex = 0;
            }
        }

        return true;
    }
}

registerProcessor('live-audio-processor', AudioProcessor);
