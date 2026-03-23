/**
 * Audio pipeline constants: sample rate, silence detection thresholds,
 * wake word timing, and live preview trigger durations.
 */
export const SAMPLE_RATE = 16_000;
export const SILENCE_THRESHOLD = 0.05;
export const SILENCE_DURATION_MS = 1_500;
export const WAKE_WORD_CHECK_INTERVAL = 500;
export const WAKE_WORD_AUDIO_WINDOW_S = 2;
export const MIN_SPEECH_DURATION_S = 0.5;
export const LIVE_PREVIEW_TRIGGER_S = 1.5;
export const WAKE_WORD_ACK_DELAY_MS = 750;
