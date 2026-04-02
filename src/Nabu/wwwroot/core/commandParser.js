import {CUSTOM_EVENTS, isMobileDevice} from './constants.js';
import {showToast} from '../ui/toast.js';

const PREFIX = '(?:(?:hey\s+)?jarvis\s*,?\s*)?';
const COMMANDS = {
    HOME: new RegExp(`^${PREFIX}(?:please\\s+)?(?:take me\\s+|go\\s+|navigate\\s+)?(?:to\\s+)?(?:the\\s+)?(home\\s*page|home)(?:\\s|$)`, 'i'),
    BACK: new RegExp(`^${PREFIX}(?:please\\s+)?(?:go\\s+|navigate\\s+|take me\\s+)?back(?:\\s|$)`, 'i'),
    RELOAD: new RegExp(`^${PREFIX}(?:please\\s+)?(?:reload|refresh)(?:\\s+(?:the\\s+)?(?:page)?)?(?:\\s|$)`, 'i'),
    SCROLL_DOWN: new RegExp(`^${PREFIX}(?:please\\s+)?(?:scroll\\s+|go\\s+|page\\s+)down(?:\\s|$)`, 'i'),
    SCROLL_UP: new RegExp(`^${PREFIX}(?:please\\s+)?(?:scroll\\s+|go\\s+|page\\s+)up(?:\\s|$)`, 'i')
};

function normalizeText(text) {
    return text.toLowerCase()
        .replace(/[.,!?;\-]/g, ' ')
        .replace(/\s+/g, ' ')
        .trim();
}

function executeWithDelay(action, message) {
    showToast(`Voice Command: ${message}`, 2000);
    setTimeout(action, 1200);
}

let isInitialized = false;

export function initCommandParser() {
    if (isInitialized) return;
    isInitialized = true;

    window.addEventListener(CUSTOM_EVENTS.TRANSCRIPTION_FINAL, (e) => {
        const text = e.detail;
        if (!text) return;

        const normalized = normalizeText(text);

        if (COMMANDS.HOME.test(normalized)) {
            console.log(`[Voice Command] HOME matched on: "${text}"`);
            executeWithDelay(() => window.location.href = '/', 'Navigating Home');
            return;
        }

        if (COMMANDS.BACK.test(normalized)) {
            console.log(`[Voice Command] BACK matched on: "${text}"`);
            executeWithDelay(() => window.history.back(), 'Going Back');
            return;
        }

        if (COMMANDS.RELOAD.test(normalized) && !isMobileDevice()) {
            console.log(`[Voice Command] RELOAD matched on: "${text}"`);
            executeWithDelay(() => window.location.reload(), 'Reloading Page');
            return;
        }

        if (COMMANDS.SCROLL_DOWN.test(normalized)) {
            console.log(`[Voice Command] SCROLL DOWN matched on: "${text}"`);
            executeWithDelay(() => window.scrollBy({
                top: window.innerHeight * 0.8,
                behavior: 'smooth'
            }), 'Scrolling Down');
            return;
        }

        if (COMMANDS.SCROLL_UP.test(normalized)) {
            console.log(`[Voice Command] SCROLL UP matched on: "${text}"`);
            executeWithDelay(() => window.scrollBy({
                top: -window.innerHeight * 0.8,
                behavior: 'smooth'
            }), 'Scrolling Up');
        }
    });
}
