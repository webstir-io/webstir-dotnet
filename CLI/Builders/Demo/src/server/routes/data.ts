// Data route for echo endpoint
import { IncomingMessage, ServerResponse } from 'http';
import type { ApiResponse } from '@shared/types/index.js';

export function handleDataRoute(req: IncomingMessage, res: ServerResponse) {
    if (req.method !== 'POST') {
        res.writeHead(405);
        res.end(JSON.stringify({ error: 'Method not allowed' }));
        return;
    }
    
    let body = '';
    req.on('data', chunk => {
        body += chunk.toString();
    });
    
    req.on('end', () => {
        try {
            const { text } = JSON.parse(body);
            
            if (!text) {
                const response: ApiResponse<null> = {
                    data: null,
                    error: 'No text provided'
                };
                res.writeHead(400);
                res.end(JSON.stringify(response));
                return;
            }
            
            const response: ApiResponse<{ echo: string }> = {
                data: {
                    echo: `You said: ${text}`
                }
            };
            
            res.writeHead(200);
            res.end(JSON.stringify(response));
        } catch (error) {
            const response: ApiResponse<null> = {
                data: null,
                error: 'Invalid JSON'
            };
            res.writeHead(400);
            res.end(JSON.stringify(response));
        }
    });
}