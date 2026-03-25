/**
 * Circular audio sample buffer. Efficiently overwrites oldest data when full,
 * enabling constant-time push and retrieval of the latest N samples.
 */
export class RingBuffer {
    constructor(capacityInSamples) {
        this.capacity = capacityInSamples;
        this.samples = new Float32Array(capacityInSamples);
        this.nextWriteIndex = 0;
        this.storedSamples = 0;
    }

    push(incomingChunk) {
        const chunkLen = incomingChunk.length;
        if (chunkLen === 0) return;

        let chunkToStore = incomingChunk;
        if (chunkLen > this.capacity) {
            chunkToStore = incomingChunk.subarray(chunkLen - this.capacity);
        }

        const len = chunkToStore.length;
        const remainingSpaceAtEnd = this.capacity - this.nextWriteIndex;

        if (len <= remainingSpaceAtEnd) {
            this.samples.set(chunkToStore, this.nextWriteIndex);
            this.nextWriteIndex = (this.nextWriteIndex + len) % this.capacity;
        } else {
            const firstPartLen = remainingSpaceAtEnd;
            const secondPartLen = len - firstPartLen;

            this.samples.set(chunkToStore.subarray(0, firstPartLen), this.nextWriteIndex);
            this.samples.set(chunkToStore.subarray(firstPartLen), 0);
            this.nextWriteIndex = secondPartLen;
        }

        this.storedSamples = Math.min(this.storedSamples + len, this.capacity);
    }

    getLatest(requestedSamples) {
        const availableSamples = Math.min(requestedSamples, this.storedSamples);
        const output = new Float32Array(availableSamples);
        if (availableSamples === 0) return output;

        this.copyLatestTo(output, availableSamples);
        return output;
    }

    copyLatestTo(targetArray, requestedSamples) {
        const availableSamples = Math.min(requestedSamples, this.storedSamples);
        if (availableSamples === 0) return 0;

        const maxToCopy = Math.min(availableSamples, targetArray.length);

        const readStartIndex = (this.nextWriteIndex - availableSamples + this.capacity) % this.capacity;
        const remainingSpaceAtEnd = this.capacity - readStartIndex;

        if (maxToCopy <= remainingSpaceAtEnd) {
            targetArray.set(this.samples.subarray(readStartIndex, readStartIndex + maxToCopy));
        } else {
            const firstPartLen = remainingSpaceAtEnd;
            const secondPartLen = maxToCopy - firstPartLen;

            targetArray.set(this.samples.subarray(readStartIndex, this.capacity), 0);
            targetArray.set(this.samples.subarray(0, secondPartLen), firstPartLen);
        }

        return maxToCopy;
    }

    get length() {
        return this.storedSamples;
    }

    clear() {
        this.nextWriteIndex = 0;
        this.storedSamples = 0;
    }
}
