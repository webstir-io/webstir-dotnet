"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
const http_1 = require("http");
const url_1 = require("url");
const apiPort = parseInt(process.env.PORT);
const webServerUrl = process.env.WEB_SERVER_URL;
const apiServerUrl = process.env.API_SERVER_URL;
const server = (0, http_1.createServer)((req, res) => {
    const url = new url_1.URL(req.url, apiServerUrl);
    res.setHeader('Access-Control-Allow-Origin', webServerUrl);
    res.setHeader('Content-Type', 'application/json');
    if (req.method === 'GET' && url.pathname === '/api/health') {
        const response = {
            data: { status: 'ok', timestamp: Date.now() }
        };
        res.writeHead(200);
        res.end(JSON.stringify(response));
    }
    else {
        const errorResponse = {
            error: 'Not found'
        };
        res.writeHead(404);
        res.end(JSON.stringify(errorResponse));
    }
});
server.listen(apiPort, () => {
    console.log(`API server running at ${apiServerUrl}`);
});
//# sourceMappingURL=index.js.map