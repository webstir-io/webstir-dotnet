const socket = new WebSocket('http://localhost:8000/ws');

socket.onopen = () => {
    console.log('WebSocket connection established.');
};

socket.onmessage = (event) => {
    console.log(event.data);
    location.reload();
};

socket.onclose = () => {
    console.log('WebSocket connection closed.');
};

window.addEventListener('beforeunload', function () {
    if (socket.readyState === WebSocket.OPEN) {
        socket.close();
    }
});