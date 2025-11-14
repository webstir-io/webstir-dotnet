import type http from 'node:http';
import pino, { stdTimeFunctions, type Logger } from 'pino';

import type { AppEnv } from '../env.js';

export function createBaseLogger(env: AppEnv): Logger {
  return pino({
    level: env.logging.level,
    base: {
      service: env.logging.serviceName,
      environment: env.NODE_ENV
    },
    timestamp: stdTimeFunctions.isoTime
  });
}

export function createRequestLogger(baseLogger: Logger, options: { requestId: string; req: http.IncomingMessage; route?: string }): Logger {
  return baseLogger.child({
    requestId: options.requestId,
    method: options.req.method ?? 'GET',
    path: options.req.url ?? '/',
    route: options.route
  });
}
