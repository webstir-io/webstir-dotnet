const eventSource = new EventSource('/sse');
let isShuttingDown = false;

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

function setConnected() {
    indicator.style.opacity = '1';
    indicator.style.background = '#4CAF50';
    indicator.textContent = '● Dev Server Connected';
}

function setDisconnected() {
    indicator.style.background = '#f44336';
    indicator.textContent = 'Dev Server Disconnected';
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

eventSource.onerror = (error) => {
    if (!isShuttingDown) {
        console.error('SSE error:', error);
        setDisconnected();
    }
};

window.addEventListener('beforeunload', function () {
    eventSource.close();
});
