import { createServer, IncomingMessage, ServerResponse } from 'http';
import { URL } from 'url';
import type { ApiResponse } from '@shared/types';

const server = createServer((req: IncomingMessage, res: ServerResponse) => {
  const url = new URL(req.url!, `http://${req.headers.host}`);
  
  // Set CORS headers for development
  res.setHeader('Access-Control-Allow-Origin', 'http://localhost:8088');
  res.setHeader('Content-Type', 'application/json');
  
  if (req.method === 'GET' && url.pathname === '/api/health') {
    const response: ApiResponse<{ status: string; timestamp: number }> = {
      data: { status: 'ok', timestamp: Date.now() }
    };
    res.writeHead(200);
    res.end(JSON.stringify(response));
  } else {
    const errorResponse: ApiResponse<never> = {
      error: 'Not found'
    };
    res.writeHead(404);
    res.end(JSON.stringify(errorResponse));
  }
});

server.listen(8000, () => {
  console.log('API server running on port 8000');
});

process.on('SIGTERM', () => {
  console.log('SIGTERM received, shutting down gracefully');
  server.close(() => {
    process.exit(0);
  });
});

process.on('SIGINT', () => {
  console.log('SIGINT received, shutting down gracefully');
  server.close(() => {
    process.exit(0);
  });
});