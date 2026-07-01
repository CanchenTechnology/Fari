class PcmProcessor extends AudioWorkletProcessor {
  constructor() {
    super();
    this.buffer = new Float32Array(0);
    this.chunkSamples = 320; // 20ms at 16kHz
  }

  process(inputs) {
    const input = inputs[0];
    if (!input || !input[0]) return true;

    const channelData = input[0];
    const next = new Float32Array(this.buffer.length + channelData.length);
    next.set(this.buffer);
    next.set(channelData, this.buffer.length);
    this.buffer = next;

    while (this.buffer.length >= this.chunkSamples) {
      const chunk = this.buffer.slice(0, this.chunkSamples);
      this.buffer = this.buffer.slice(this.chunkSamples);

      const int16 = new Int16Array(chunk.length);
      for (let i = 0; i < chunk.length; i += 1) {
        const sample = Math.max(-1, Math.min(1, chunk[i]));
        int16[i] = sample < 0 ? sample * 0x8000 : sample * 0x7fff;
      }

      this.port.postMessage(int16.buffer, [int16.buffer]);
    }

    return true;
  }
}

registerProcessor("pcm-processor", PcmProcessor);

