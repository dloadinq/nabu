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
    Object.assign(toast.style, {
        position: 'fixed',
        bottom: '24px',
        left: '50%',
        transform: 'translateX(-50%) translateY(20px)',
        background: 'rgba(30, 30, 30, 0.95)',
        color: '#fff',
        padding: '12px 16px 12px 24px',
        borderRadius: '8px',
        fontSize: '14px',
        fontFamily: 'system-ui, sans-serif',
        zIndex: '10000',
        opacity: '0',
        transition: 'opacity 0.3s ease, transform 0.3s ease',
        pointerEvents: 'auto',
        maxWidth: '90vw',
        textAlign: 'left',
        boxShadow: '0 8px 24px rgba(0,0,0,0.4)',
        display: 'flex',
        alignItems: 'center',
        gap: '16px'
    });

    const msgSpan = document.createElement('span');
    msgSpan.textContent = message;
    toast.appendChild(msgSpan);

    const closeBtn = document.createElement('button');
    closeBtn.innerHTML = '&times;';
    Object.assign(closeBtn.style, {
        background: 'transparent',
        border: 'none',
        color: 'rgba(255, 255, 255, 0.5)',
        fontSize: '20px',
        lineHeight: '1',
        cursor: 'pointer',
        padding: '0',
        marginLeft: 'auto'
    });
    
    closeBtn.onmouseover = () => closeBtn.style.color = '#fff';
    closeBtn.onmouseout = () => closeBtn.style.color = 'rgba(255, 255, 255, 0.5)';

    const removeToast = () => {
        toast.style.opacity = '0';
        toast.style.transform = 'translateX(-50%) translateY(20px)';
        toast.addEventListener('transitionend', () => {
            if (currentToast === toast) currentToast = null;
            toast.remove();
        }, { once: true });
    };

    closeBtn.onclick = removeToast;
    toast.appendChild(closeBtn);

    document.body.appendChild(toast);
    currentToast = toast;

    requestAnimationFrame(() => {
        toast.style.opacity = '1';
        toast.style.transform = 'translateX(-50%) translateY(0)';
    });

    currentToastTimeout = setTimeout(removeToast, durationMs);
}
