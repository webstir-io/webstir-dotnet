// Webstir Demo Server
// This demonstrates a Node.js API server with TypeScript using built-in modules

import { createServer, IncomingMessage, ServerResponse } from 'http';
import { URL } from 'url';
import type { ApiResponse } from '@shared/types/index.js';
import type { User, Feature } from '@shared/types/demo.js';
import { handleUsersRoute } from './routes/users.js';
import { handleDataRoute } from './routes/data.js';

const server = createServer(async (req: IncomingMessage, res: ServerResponse) => {
    const url = new URL(req.url!, `http://${req.headers.host}`);
    
    // Set CORS headers for development
    res.setHeader('Access-Control-Allow-Origin', 'http://localhost:8088');
    res.setHeader('Content-Type', 'application/json');
    
    // Handle preflight requests
    if (req.method === 'OPTIONS') {
        res.setHeader('Access-Control-Allow-Methods', 'GET, POST, PUT, DELETE');
        res.setHeader('Access-Control-Allow-Headers', 'Content-Type');
        res.writeHead(200);
        res.end();
        return;
    }
    
    // Route handling
    if (url.pathname.startsWith('/api/users')) {
        handleUsersRoute(req, res, url);
    } else if (url.pathname === '/api/data' && req.method === 'POST') {
        handleDataRoute(req, res);
    } else if (url.pathname === '/api/features' && req.method === 'GET') {
        const features: Feature[] = [
            { id: '1', name: 'TypeScript Support', description: 'Full TypeScript support for client and server' },
            { id: '2', name: 'Hot Reload', description: 'Instant updates during development' },
            { id: '3', name: 'API Proxy', description: 'Seamless API integration' },
            { id: '4', name: 'Shared Types', description: 'Type safety across the stack' },
            { id: '5', name: 'SPA Routing', description: 'Optional client-side routing' },
            { id: '6', name: 'Build System', description: 'Fast, reliable builds' }
        ];
        
        const response: ApiResponse<Feature[]> = {
            data: features
        };
        
        res.writeHead(200);
        res.end(JSON.stringify(response));
    } else {
        const errorResponse: ApiResponse<null> = {
            data: null,
            error: 'Not found'
        };
        res.writeHead(404);
        res.end(JSON.stringify(errorResponse));
    }
});

server.listen(3001, () => {
    console.log('Webstir demo API server running on port 3001');
});