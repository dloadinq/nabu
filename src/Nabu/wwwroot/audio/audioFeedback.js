let chimeAudio = null;

function getChimeAudio() {
    if (chimeAudio) return chimeAudio;

    chimeAudio = new Audio('/_content/Nabu/audio/ui-chime.mp3');
    chimeAudio.volume = 0.8;

    return chimeAudio;
}

export function playWakeWordFeedback() {
    const audio = getChimeAudio();
    audio.currentTime = 0;
    audio.play().catch((playbackError) => console.warn('Could not play wake word chime:', playbackError));
}
