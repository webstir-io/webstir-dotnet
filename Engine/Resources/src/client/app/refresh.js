const eventSource = new EventSource('/sse');
let isShuttingDown = false;

eventSource.onopen = () => {
    console.log('SSE connection established.');
};

eventSource.onmessage = (event) => {
    console.log('Received:', event.data);
    if (event.data === 'reload') {
        location.reload();
    } else if (event.data === 'shutdown') {
        isShuttingDown = true;
        eventSource.close();
    }
};

eventSource.onerror = (error) => {
    if (!isShuttingDown) {
        console.error('SSE error:', error);
        // EventSource will automatically reconnect
    }
};

window.addEventListener('beforeunload', function () {
    eventSource.close();
});