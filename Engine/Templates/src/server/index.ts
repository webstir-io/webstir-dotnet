import { createServer, IncomingMessage, ServerResponse } from 'http';
import { URL } from 'url';
import type { ApiResponse } from '@shared/types';

const apiPort = parseInt(process.env.PORT!);
const webServerUrl = process.env.WEB_SERVER_URL!;
const apiServerUrl = process.env.API_SERVER_URL!;

const server = createServer((req: IncomingMessage, res: ServerResponse) => {
  const url = new URL(req.url!, apiServerUrl);
  
  // Set CORS headers for development
  res.setHeader('Access-Control-Allow-Origin', webServerUrl);
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

server.listen(apiPort, () => {
  console.log(`API server running at ${apiServerUrl}`);
});