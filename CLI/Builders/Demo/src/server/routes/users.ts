// Users route demonstrating shared types
import { IncomingMessage, ServerResponse } from 'http';
import { URL } from 'url';
import type { ApiResponse } from '@shared/types/index.js';
import type { User } from '@shared/types/demo.js';

// Mock users data
const users: User[] = [
    { id: '1', name: 'Alice Johnson', email: 'alice@example.com' },
    { id: '2', name: 'Bob Smith', email: 'bob@example.com' },
    { id: '3', name: 'Charlie Brown', email: 'charlie@example.com' }
];

export function handleUsersRoute(req: IncomingMessage, res: ServerResponse, url: URL) {
    const pathParts = url.pathname.split('/').filter(Boolean);
    
    if (req.method === 'GET') {
        // GET /api/users
        if (pathParts.length === 2) {
            const response: ApiResponse<User[]> = {
                data: users
            };
            res.writeHead(200);
            res.end(JSON.stringify(response));
        }
        // GET /api/users/:id
        else if (pathParts.length === 3) {
            const userId = pathParts[2];
            const user = users.find(u => u.id === userId);
            
            if (!user) {
                const response: ApiResponse<null> = {
                    data: null,
                    error: 'User not found'
                };
                res.writeHead(404);
                res.end(JSON.stringify(response));
                return;
            }
            
            const response: ApiResponse<User> = {
                data: user
            };
            res.writeHead(200);
            res.end(JSON.stringify(response));
        }
    } else {
        res.writeHead(405);
        res.end(JSON.stringify({ error: 'Method not allowed' }));
    }
}