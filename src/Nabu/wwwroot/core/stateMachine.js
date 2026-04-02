export const STATES = Object.freeze({
    IDLE: 'IDLE',
    LISTENING: 'LISTENING',
    KW_DETECTED: 'KW_DETECTED',
    TRANSCRIBING: 'TRANSCRIBING',
    FLUSHING: 'FLUSHING',
    STOPPED: 'STOPPED',
});

const ALLOWED_TRANSITIONS = new Map([
    [STATES.IDLE, new Set([STATES.LISTENING, STATES.STOPPED])],
    [STATES.LISTENING, new Set([STATES.KW_DETECTED, STATES.STOPPED])],
    [STATES.KW_DETECTED, new Set([STATES.TRANSCRIBING, STATES.LISTENING, STATES.STOPPED])],
    [STATES.TRANSCRIBING, new Set([STATES.FLUSHING, STATES.LISTENING, STATES.STOPPED])],
    [STATES.FLUSHING, new Set([STATES.LISTENING, STATES.STOPPED])],
    [STATES.STOPPED, new Set([STATES.IDLE, STATES.LISTENING])],
]);

const timestamp = () => new Date().toISOString().substring(11, 23);

export class StateMachine {
    constructor(onTransition = () => {
    }) {
        this.currentState = STATES.IDLE;
        this.onTransitionCallback = onTransition;
    }

    get current() {
        return this.currentState;
    }

    is(state) {
        return this.currentState === state;
    }

    transition(targetState, requiredCurrentState = null) {
        if (requiredCurrentState !== null && this.currentState !== requiredCurrentState) {
            console.warn(
                `[${timestamp()}] StateMachine: ${this.currentState}→${targetState} blocked – expected ${requiredCurrentState}`
            );
            return false;
        }

        const allowedTargetStates = ALLOWED_TRANSITIONS.get(this.currentState);
        if (!allowedTargetStates || !allowedTargetStates.has(targetState)) {
            console.warn(`[${timestamp()}] StateMachine: Illegal transition ${this.currentState}→${targetState}`);
            return false;
        }

        this.currentState = targetState;
        this.onTransitionCallback(targetState);
        return true;
    }

    transitionFrom(requiredCurrentState, targetState) {
        return this.transition(targetState, requiredCurrentState);
    }

    reset() {
        this.currentState = STATES.IDLE;
        this.onTransitionCallback(STATES.IDLE);
    }
}
