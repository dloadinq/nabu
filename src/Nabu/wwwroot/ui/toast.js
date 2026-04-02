let currentToast = null;
let currentToastTimeout = null;

export function hasActiveToast() {
    return currentToast !== null;
}

export function showToast(message, durationMs = 5000) {
    if (currentToast) {
        currentToast.remove();
        if (currentToastTimeout) clearTimeout(currentToastTimeout);
    }

    const toast = document.createElement('div');
    toast.className = 'nabu-toast';

    toast.innerHTML = `
        <span>${message}</span>
        <button class="nabu-toast__close">&times;</button>
    `;

    document.body.appendChild(toast);
    currentToast = toast;

    requestAnimationFrame(() => {
        toast.classList.add('nabu-toast--active');
    });

    const removeToast = () => {
        if (currentToastTimeout) {
            clearTimeout(currentToastTimeout);
            currentToastTimeout = null;
        }

        toast.classList.remove('nabu-toast--active');

        toast.addEventListener('transitionend', () => {
            if (currentToast === toast) currentToast = null;
            toast.remove();
        }, {once: true});
    };

    toast.querySelector('.nabu-toast__close').onclick = removeToast;
    currentToastTimeout = setTimeout(removeToast, durationMs);
}
