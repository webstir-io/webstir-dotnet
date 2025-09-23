const existingEventSource = window.__webstirEventSource;
const eventSource = existingEventSource instanceof EventSource
    ? existingEventSource
    : new EventSource('/sse');
window.__webstirEventSource = eventSource;
let isShuttingDown = false;
let resetTimer;

const indicator = document.createElement('div');
indicator.id = 'dev-server-indicator';
indicator.style.cssText = `
    position: fixed;
    bottom: 20px;
    right: 20px;
    color: white;
    padding: 12px 16px;
    border-radius: 20px;
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
    font-size: 12px;
    font-weight: 500;
    z-index: 10000;
    box-shadow: 0 2px 8px rgba(0,0,0,0.15);
    opacity: 0;
    transition: opacity 0.3s ease;
`;

document.body.appendChild(indicator);

function updateIndicator(background, text, shouldReset = false) {
    indicator.style.opacity = '1';
    indicator.style.background = background;
    indicator.textContent = text;

    if (resetTimer) {
        clearTimeout(resetTimer);
        resetTimer = undefined;
    }

    if (shouldReset) {
        resetTimer = setTimeout(setConnected, 1500);
    }
}

function setConnected(message) {
    updateIndicator('#4CAF50', message ?? '● Dev Server Connected');
}

function setDisconnected(message) {
    updateIndicator('#f44336', message ?? 'Dev Server Disconnected');
}

function setBuilding(message) {
    updateIndicator('#FF9800', message ?? '● Rebuilding…');
}

function setBuildSuccess(message) {
    updateIndicator('#4CAF50', message ?? '● Rebuild Complete', true);
}

function setBuildFailure(message) {
    updateIndicator('#f44336', message ?? '● Build Failed');
}

function setHmrFallback(message) {
    updateIndicator('#FF5722', message ?? '● Reloading (HMR fallback)…');
}

const statusHandlers = {
    connected: setConnected,
    disconnected: setDisconnected,
    building: setBuilding,
    success: setBuildSuccess,
    error: setBuildFailure,
    'hmr-fallback': setHmrFallback
};

function applyStatus(status, message) {
    const handler = statusHandlers[status];
    if (typeof handler === 'function') {
        handler(message);
    }
}

window.__webstirSetDevStatus = applyStatus;

eventSource.onopen = () => {
    console.log('SSE connection established.');
    applyStatus('connected');
};

eventSource.onmessage = (event) => {
    if (event.data === 'reload') {
        location.reload();
    } else if (event.data === 'shutdown') {
        isShuttingDown = true;
        setDisconnected();
        eventSource.close();
    }
};

eventSource.addEventListener('status', (event) => {
    applyStatus(event.data);
});

eventSource.onerror = (error) => {
    if (!isShuttingDown) {
        console.error('SSE error:', error);
        applyStatus('disconnected');
    }
};

window.addEventListener('beforeunload', function () {
    eventSource.close();
});
