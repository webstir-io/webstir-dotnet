const eventSource = new EventSource('http://localhost:8000/events');

eventSource.onopen = () => {
    console.log('SSE connection established.');
};

eventSource.onmessage = (event) => {
    console.log('Received:', event.data);
    if (event.data === 'reload') {
        location.reload();
    }
};

eventSource.onerror = (error) => {
    console.error('SSE error:', error);
    // EventSource will automatically reconnect
};

window.addEventListener('beforeunload', function () {
    eventSource.close();
});