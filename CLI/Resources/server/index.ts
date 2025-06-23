import { createServer, IncomingMessage, ServerResponse } from 'http';
import { URL } from 'url';

const server = createServer((req: IncomingMessage, res: ServerResponse) => {
  const url = new URL(req.url!, `http://${req.headers.host}`);
  
  // Set CORS headers for development
  res.setHeader('Access-Control-Allow-Origin', 'http://localhost:8088');
  res.setHeader('Content-Type', 'application/json');
  
  if (req.method === 'GET' && url.pathname === '/api/health') {
    res.writeHead(200);
    res.end(JSON.stringify({ status: 'ok', timestamp: Date.now() }));
  } else {
    res.writeHead(404);
    res.end(JSON.stringify({ error: 'Not found' }));
  }
});

server.listen(3001, () => {
  console.log('API server running on port 3001');
});