const eventSource = new EventSource('/sse');
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

function setConnected() {
    updateIndicator('#4CAF50', '● Dev Server Connected');
}

function setDisconnected() {
    updateIndicator('#f44336', 'Dev Server Disconnected');
}

function setBuilding() {
    updateIndicator('#FF9800', '● Rebuilding…');
}

function setBuildSuccess() {
    updateIndicator('#4CAF50', '● Rebuild Complete', true);
}

function setBuildFailure() {
    updateIndicator('#f44336', '● Build Failed');
}

eventSource.onopen = () => {
    console.log('SSE connection established.');
    setConnected();
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
    switch (event.data) {
        case 'building':
            setBuilding();
            break;
        case 'success':
            setBuildSuccess();
            break;
        case 'error':
            setBuildFailure();
            break;
        default:
            break;
    }
});

eventSource.onerror = (error) => {
    if (!isShuttingDown) {
        console.error('SSE error:', error);
        setDisconnected();
    }
};

window.addEventListener('beforeunload', function () {
    eventSource.close();
});
