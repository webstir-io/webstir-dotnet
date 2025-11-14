// Optional Fastify server scaffold for richer routing
// Rename or import into your backend index to use.
import Fastify from 'fastify';
import { randomUUID } from 'node:crypto';

import { loadEnv } from '../env.js';

type LogLevel = 'trace' | 'debug' | 'info' | 'warn' | 'error';

interface Logger {
  readonly level: LogLevel;
  log(level: LogLevel, message: string, metadata?: Record<string, unknown>): void;
  debug(message: string, metadata?: Record<string, unknown>): void;
  info(message: string, metadata?: Record<string, unknown>): void;
  warn(message: string, metadata?: Record<string, unknown>): void;
  error(message: string, metadata?: Record<string, unknown>): void;
  with(bindings: Record<string, unknown>): Logger;
}

interface EnvAccessor {
  get(name: string): string | undefined;
  require(name: string): string;
  entries(): Record<string, string | undefined>;
}

interface ModuleRouteDefinition {
  name?: string;
  method?: string;
  path?: string;
}

interface ModuleRoute {
  definition?: ModuleRouteDefinition;
  handler?: (ctx: Record<string, unknown>) => Promise<any> | any;
}

interface ModuleManifestLike {
  name?: string;
  version?: string;
  capabilities?: string[];
  routes?: ModuleRouteDefinition[];
}

interface ModuleDefinitionLike {
  manifest?: ModuleManifestLike;
  routes?: ModuleRoute[];
}

interface ManifestSummary {
  name?: string;
  version?: string;
  routes: number;
  capabilities?: string[];
}

type ReadinessStatus = 'booting' | 'ready' | 'error';
type ReadinessTracker = ReturnType<typeof createReadinessTracker>;

export async function start(): Promise<void> {
  const env = loadEnv();
  const port = env.PORT;
  const mode = env.NODE_ENV;
  const readiness = createReadinessTracker();
  readiness.booting();

  const app = Fastify({ logger: false });

  app.get('/api/health', async () => ({ ok: true, uptime: process.uptime() }));
  app.get('/healthz', async () => ({ ok: true }));

  let manifestSummary: ManifestSummary | undefined;

  app.get('/readyz', async (_req, reply) => {
    const snapshot = readiness.snapshot();
    const statusCode = snapshot.status === 'ready' ? 200 : 503;
    reply.code(statusCode);
    return { status: snapshot.status, message: snapshot.message, manifest: manifestSummary };
  });

  try {
    const definition = await tryLoadModuleDefinition();
    if (definition) {
      manifestSummary = summarizeManifest(definition.manifest, definition.routes);
      logManifestSummary(definition.manifest, definition.routes);
      mountRoutes(app, definition);
    } else {
      console.info('[fastify] no module definition found. Routes will be empty.');
    }
  } catch (error) {
    readiness.error((error as Error).message ?? 'module load failed');
    console.error('[fastify] failed to load module definition:', error);
  }

  await app.listen({ port, host: '0.0.0.0' });

  if (readiness.snapshot().status !== 'error') {
    readiness.ready();
  }

  // Dev runner watches for this readiness line
  console.info('API server running');
  console.info(`[webstir-backend] mode=${mode} port=${port}`);
}

function mountRoutes(app: import('fastify').FastifyInstance, definition: ModuleDefinitionLike) {
  const routes = Array.isArray(definition?.routes) ? definition.routes : [];
  for (const r of routes) {
    try {
      const method = String(r.definition?.method ?? 'GET').toUpperCase();
      const url = String(r.definition?.path ?? '/');
      const handler = r.handler;
      if (typeof handler !== 'function') continue;

      app.route({
        method: method as any,
        url,
        handler: async (req, reply) => {
          const requestId = extractRequestId(req);
          reply.header('x-request-id', requestId);
          const envAccessor = createEnvAccessor();
          const ctx: Record<string, unknown> = {
            request: req,
            reply,
            auth: undefined,
            session: null,
            db: {},
            env: envAccessor,
            logger: createRequestLogger(requestId),
            requestId,
            now: () => new Date(),
            params: (req as any).params ?? {},
            query: (req as any).query ?? {},
            body: (req as any).body ?? {}
          };
          const result = await handler(ctx);
          const status = result?.status ?? (result?.errors ? 400 : 200);
          const headers = result?.headers ?? { 'content-type': 'application/json' };
          for (const [k, v] of Object.entries(headers)) {
            reply.header(k, String(v));
          }
          if (result?.errors) {
            reply.code(status).send({ errors: result.errors });
          } else {
            reply.code(status).send(result?.body ?? null);
          }
        }
      });
      console.info(`[fastify] mounted ${method} ${url}`);
    } catch (error) {
      console.warn('[fastify] failed to mount route', error);
    }
  }
}

async function tryLoadModuleDefinition(): Promise<ModuleDefinitionLike | undefined> {
  const candidates = ['../module.js', '../module/index.js'];
  for (const rel of candidates) {
    try {
      const url = new URL(rel, import.meta.url);
      const mod = await import(url.toString());
      const def = (mod && (mod.module || mod.moduleDefinition || mod.default)) as ModuleDefinitionLike;
      if (def && typeof def === 'object') return def;
    } catch {
      // ignore and try next
    }
  }
  return undefined;
}

function summarizeManifest(manifest?: ModuleManifestLike, routes?: ModuleRoute[]): ManifestSummary | undefined {
  if (!manifest) return undefined;
  const routeCount = Array.isArray(manifest.routes) ? manifest.routes.length : Array.isArray(routes) ? routes.length : 0;
  return {
    name: manifest.name,
    version: manifest.version,
    routes: routeCount,
    capabilities: manifest.capabilities && manifest.capabilities.length > 0 ? manifest.capabilities : undefined
  };
}

function logManifestSummary(manifest: ModuleManifestLike | undefined, routes?: ModuleRoute[]): void {
  if (!manifest) {
    console.info('[fastify] manifest metadata not found.');
    return;
  }
  const caps = manifest.capabilities?.length ? ` [${manifest.capabilities.join(', ')}]` : '';
  const count = Array.isArray(manifest.routes) ? manifest.routes.length : Array.isArray(routes) ? routes.length : 0;
  console.info(`[fastify] manifest name=${manifest.name ?? 'unknown'} routes=${count}${caps}`);
}

function createEnvAccessor(): EnvAccessor {
  return {
    get: (name) => process.env[name],
    require: (name) => {
      const value = process.env[name];
      if (value === undefined) {
        throw new Error(`Missing required env var ${name}`);
      }
      return value;
    },
    entries: () => ({ ...process.env })
  };
}

function createRequestLogger(requestId: string, bindings: Record<string, unknown> = {}): Logger {
  const logWithLevel = (level: LogLevel, message: string, metadata?: Record<string, unknown>) => {
    const bindingKeys = Object.keys(bindings);
    const suffix = bindingKeys.length ? ` ${bindingKeys.map((k) => `${k}=${JSON.stringify(bindings[k])}`).join(' ')}` : '';
    const writer = level === 'error' ? console.error : level === 'warn' ? console.warn : console.log;
    if (metadata) {
      writer(`[${level}] [request ${requestId}] ${message}${suffix}`, metadata);
    } else {
      writer(`[${level}] [request ${requestId}] ${message}${suffix}`);
    }
  };

  return {
    level: 'info',
    log: logWithLevel,
    debug: (message, metadata) => logWithLevel('debug', message, metadata),
    info: (message, metadata) => logWithLevel('info', message, metadata),
    warn: (message, metadata) => logWithLevel('warn', message, metadata),
    error: (message, metadata) => logWithLevel('error', message, metadata),
    with(extra) {
      return createRequestLogger(requestId, { ...bindings, ...extra });
    }
  };
}

function extractRequestId(req: { id?: string; headers?: Record<string, unknown> }): string {
  if (req && typeof req.id === 'string' && req.id.length > 0) {
    return req.id;
  }
  const header = req?.headers?.['x-request-id'];
  if (typeof header === 'string' && header.length > 0) {
    return header;
  }
  if (Array.isArray(header) && header.length > 0) {
    return header[0] as string;
  }
  try {
    return randomUUID();
  } catch {
    return `${Date.now()}`;
  }
}

function createReadinessTracker() {
  let status: ReadinessStatus = 'booting';
  let message: string | undefined;
  return {
    booting() {
      status = 'booting';
      message = undefined;
    },
    ready() {
      status = 'ready';
      message = undefined;
    },
    error(reason: string) {
      status = 'error';
      message = reason;
    },
    snapshot() {
      return { status, message };
    }
  };
}

// Execute when launched directly
const isMain = (() => {
  try {
    const argv1 = process.argv?.[1];
    if (!argv1) return false;
    const here = new URL(import.meta.url);
    const run = new URL(`file://${argv1}`);
    return here.pathname === run.pathname;
  } catch {
    return false;
  }
})();

if (isMain) {
  start().catch((err) => {
    console.error(err);
    process.exitCode = 1;
  });
}
