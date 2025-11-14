import http from 'node:http';
import path from 'node:path';
import { randomUUID } from 'node:crypto';
import { fileURLToPath, pathToFileURL } from 'node:url';

import type { Logger } from 'pino';

import { loadEnv, type AppEnv } from './env.js';
import { resolveRequestAuth, type AuthContext } from './auth/adapter.js';
import { createBaseLogger, createRequestLogger } from './observability/logger.js';
import { createMetricsTracker, type MetricsTracker } from './observability/metrics.js';

interface EnvAccessor {
  get(name: string): string | undefined;
  require(name: string): string;
  entries(): Record<string, string | undefined>;
}

type RouteHandlerResult = {
  status?: number;
  headers?: Record<string, string>;
  body?: unknown;
  errors?: { code: string; message: string; details?: unknown }[];
};

interface RouteContext {
  request: http.IncomingMessage;
  reply: http.ServerResponse;
  params: Record<string, string>;
  query: Record<string, string>;
  body: unknown;
  auth: AuthContext | undefined;
  session: null;
  db: Record<string, unknown>;
  env: EnvAccessor;
  logger: Logger;
  requestId: string;
  now: () => Date;
}

type RouteHandler = (ctx: RouteContext) => Promise<RouteHandlerResult> | RouteHandlerResult;

interface ModuleRouteDefinition {
  name?: string;
  method?: string;
  path?: string;
}

interface ModuleRoute {
  definition?: ModuleRouteDefinition;
  handler?: RouteHandler;
}

interface ModuleManifestLike {
  name?: string;
  version?: string;
  capabilities?: string[];
  routes?: ModuleRouteDefinition[];
}

type LifecycleHook = (context: { env: EnvAccessor; logger: Logger }) => Promise<void> | void;

interface ModuleDefinitionLike {
  manifest?: ModuleManifestLike;
  routes?: ModuleRoute[];
  init?: LifecycleHook;
  dispose?: LifecycleHook;
}

interface CompiledRoute {
  method: string;
  name: string;
  match: (pathname: string) => { matched: boolean; params: Record<string, string> };
  handler: RouteHandler;
  definition?: ModuleRouteDefinition;
}

interface ModuleRuntime {
  definition?: ModuleDefinitionLike;
  manifest?: ModuleManifestLike;
  routes: CompiledRoute[];
  source?: string;
}

type ReadinessStatus = 'booting' | 'ready' | 'error';

interface ReadinessState {
  status: ReadinessStatus;
  message?: string;
}

type ReadinessTracker = ReturnType<typeof createReadinessTracker>;

interface ManifestSummary {
  name?: string;
  version?: string;
  routes: number;
  capabilities?: string[];
}

export async function start(): Promise<void> {
  const env = loadEnv();
  const logger = createBaseLogger(env);
  const metrics = createMetricsTracker(env.metrics);
  const readiness = createReadinessTracker();
  readiness.booting();

  let runtime: ModuleRuntime;
  let loadError: string | undefined;

  try {
    runtime = await loadModuleRuntime();
  } catch (error) {
    loadError = (error as Error).message ?? 'Failed to load module definition';
    logger.error({ err: error }, '[webstir-backend] module load failed');
    readiness.error(loadError);
    runtime = { routes: [] };
  }

  if (runtime.source) {
    logger.info(`[webstir-backend] loaded module definition from ${runtime.source}`);
  } else {
    logger.warn('[webstir-backend] no module definition found. Add src/backend/module.ts to describe routes.');
  }

  logManifestSummary(logger, runtime.manifest, runtime.routes.length);
  const manifestSummary = summarizeManifest(runtime.manifest);

  const server = http.createServer((req, res) => {
    void handleRequest({ req, res, runtime, readiness, manifestSummary, env, logger, metrics });
  });

  await new Promise<void>((resolve, reject) => {
    server.once('error', reject);
    server.listen(env.PORT, '0.0.0.0', () => resolve());
  });

  if (!loadError) {
    readiness.ready();
  }

  logger.info({ port: env.PORT, mode: env.NODE_ENV }, 'API server running');
}

async function handleRequest(options: {
  req: http.IncomingMessage;
  res: http.ServerResponse;
  runtime: ModuleRuntime;
  readiness: ReadinessTracker;
  env: AppEnv;
  logger: Logger;
  metrics: MetricsTracker;
  manifestSummary?: ManifestSummary;
}): Promise<void> {
  const { req, res, runtime, readiness, manifestSummary, env, logger, metrics } = options;
  try {
    if (!req.url) {
      respondJson(res, 400, { error: 'bad_request' });
      return;
    }

    const url = new URL(req.url, `http://${req.headers.host ?? 'localhost'}`);
    const pathname = normalizePath(url.pathname);
    const method = (req.method ?? 'GET').toUpperCase();

    if (isHealthPath(pathname)) {
      respondJson(res, 200, { ok: true, uptime: process.uptime() });
      return;
    }

    if (isReadyPath(pathname)) {
      const snapshot = readiness.snapshot();
      const statusCode = snapshot.status === 'ready' ? 200 : 503;
      respondJson(res, statusCode, {
        status: snapshot.status,
        message: snapshot.message,
        manifest: manifestSummary,
        metrics: metrics.snapshot()
      });
      return;
    }

    if (isMetricsPath(pathname)) {
      const snapshot = metrics.snapshot();
      respondJson(res, 200, snapshot ?? { enabled: false });
      return;
    }

    if (method === 'OPTIONS') {
      res.statusCode = 204;
      res.setHeader('Access-Control-Allow-Origin', req.headers.origin ?? '*');
      res.setHeader('Access-Control-Allow-Methods', 'GET,HEAD,POST,PUT,PATCH,DELETE,OPTIONS');
      res.setHeader('Access-Control-Allow-Headers', req.headers['access-control-request-headers'] ?? 'content-type');
      res.end('');
      return;
    }

    const matched = matchRoute(runtime.routes, method, pathname);
    if (!matched) {
      respondJson(res, 404, { error: 'not_found', path: pathname });
      metrics.record({ method, route: pathname, status: 404, durationMs: 0 });
      return;
    }

    const routeName = matched.route.name ?? matched.route.definition?.path ?? pathname;
    const startTime = process.hrtime.bigint();
    const body = await readRequestBody(req);
    const requestId = extractRequestId(req);
    res.setHeader('x-request-id', requestId);

    const requestLogger = createRequestLogger(logger, { requestId, req, route: routeName });
    const envAccessor = createEnvAccessor();
    const auth = resolveRequestAuth(req, env.auth, requestLogger);
    const db: Record<string, unknown> = Object.create(null);
    const ctx: RouteContext = {
      request: req,
      reply: res,
      params: matched.params,
      query: Object.fromEntries(url.searchParams.entries()),
      body,
      auth,
      session: null,
      db,
      env: envAccessor,
      logger: requestLogger,
      requestId,
      now: () => new Date()
    };

    let handlerFailed = false;
    try {
      const result = await matched.route.handler(ctx);
      sendRouteResponse(res, result);
    } catch (error) {
      handlerFailed = true;
      requestLogger.error({ err: error }, 'route handler failed');
      throw error;
    } finally {
      const durationMs = Number(process.hrtime.bigint() - startTime) / 1_000_000;
      const statusCode = handlerFailed ? 500 : res.statusCode ?? 200;
      metrics.record({ method, route: routeName, status: statusCode, durationMs });
      requestLogger.info({ status: statusCode, durationMs }, 'request.completed');
    }
  } catch (error) {
    logger.error({ err: error }, '[webstir-backend] request failed');
    if (!res.headersSent) {
      respondJson(res, 500, { error: 'internal_error', message: (error as Error).message });
    } else {
      res.end();
    }
  }
}

function sendRouteResponse(res: http.ServerResponse, result: RouteHandlerResult): void {
  const status = result.status ?? (result.errors ? 400 : 200);
  const headers = result.headers ?? { 'content-type': 'application/json' };
  res.statusCode = status;
  for (const [key, value] of Object.entries(headers)) {
    res.setHeader(key, value);
  }

  if (result.errors) {
    respondJson(res, status, { errors: result.errors });
    return;
  }

  if (result.body === undefined || result.body === null) {
    res.end('');
    return;
  }

  if (typeof result.body === 'string' || Buffer.isBuffer(result.body)) {
    res.end(result.body);
    return;
  }

  respondJson(res, status, result.body);
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

function extractRequestId(req: http.IncomingMessage): string {
  const header = req.headers['x-request-id'];
  if (typeof header === 'string' && header.length > 0) {
    return header;
  }
  if (Array.isArray(header) && header.length > 0) {
    return header[0];
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
    snapshot(): ReadinessState {
      return { status, message };
    }
  };
}

function isHealthPath(pathname: string): boolean {
  return pathname === '/api/health' || pathname === '/healthz';
}

function isReadyPath(pathname: string): boolean {
  return pathname === '/readyz';
}

function isMetricsPath(pathname: string): boolean {
  return pathname === '/metrics';
}

function respondJson(res: http.ServerResponse, status: number, payload: unknown): void {
  if (!res.headersSent) {
    if (!res.hasHeader('content-type')) {
      res.setHeader('Content-Type', 'application/json');
    }
    res.statusCode = status;
  }
  res.end(JSON.stringify(payload));
}

function matchRoute(routes: CompiledRoute[], method: string, pathname: string): { route: CompiledRoute; params: Record<string, string> } | undefined {
  const normalizedMethod = (method ?? 'GET').toUpperCase();
  for (const route of routes) {
    if (route.method !== normalizedMethod) continue;
    const { matched, params } = route.match(pathname);
    if (matched) {
      return { route, params };
    }
  }
  return undefined;
}

async function loadModuleRuntime(): Promise<ModuleRuntime> {
  const loaded = await tryLoadModuleDefinition();
  if (!loaded) {
    return { routes: [] };
  }
  const manifest = sanitizeManifest(loaded.definition.manifest);
  const routes = compileRoutes(loaded.definition.routes ?? []);
  return {
    definition: loaded.definition,
    manifest,
    routes,
    source: loaded.source
  };
}

function sanitizeManifest(manifest?: ModuleManifestLike): ModuleManifestLike | undefined {
  if (!manifest || typeof manifest !== 'object') {
    return undefined;
  }
  return {
    ...manifest,
    routes: Array.isArray(manifest.routes) ? manifest.routes : [],
    capabilities: Array.isArray(manifest.capabilities) ? manifest.capabilities : undefined
  };
}

function summarizeManifest(manifest?: ModuleManifestLike): ManifestSummary | undefined {
  if (!manifest) {
    return undefined;
  }
  return {
    name: manifest.name,
    version: manifest.version,
    routes: Array.isArray(manifest.routes) ? manifest.routes.length : 0,
    capabilities: manifest.capabilities && manifest.capabilities.length > 0 ? manifest.capabilities : undefined
  };
}

function logManifestSummary(logger: Logger, manifest: ModuleManifestLike | undefined, routeCount: number): void {
  if (!manifest) {
    logger.info(`[webstir-backend] manifest routes=${routeCount} (no manifest metadata found)`);
    return;
  }
  const caps = manifest.capabilities?.length ? ` [${manifest.capabilities.join(', ')}]` : '';
  const routes = Array.isArray(manifest.routes) ? manifest.routes.length : routeCount;
  logger.info(`[webstir-backend] manifest name=${manifest.name ?? 'unknown'} routes=${routes}${caps}`);
}

function compileRoutes(routes: ModuleRoute[]): CompiledRoute[] {
  const compiled: CompiledRoute[] = [];
  for (const route of routes) {
    if (typeof route.handler !== 'function') {
      continue;
    }
    const method = (route.definition?.method ?? 'GET').toUpperCase();
    const pathPattern = normalizePath(route.definition?.path ?? '/');
    compiled.push({
      method,
      name: route.definition?.name ?? pathPattern,
      match: createPathMatcher(pathPattern),
      handler: route.handler,
      definition: route.definition
    });
  }
  return compiled;
}

function createPathMatcher(pattern: string) {
  const normalized = normalizePath(pattern);
  const paramRegex = /:([A-Za-z0-9_]+)/g;
  const regex = new RegExp(
    '^' +
      normalized
        .replace(/\//g, '\\/')
        .replace(paramRegex, (_segment, name) => `(?<${name}>[^/]+)`) +
      '$'
  );
  return (pathname: string) => {
    const pathToTest = normalizePath(pathname);
    const match = regex.exec(pathToTest);
    if (!match) {
      return { matched: false, params: {} };
    }
    const params = (match.groups ?? {}) as Record<string, string>;
    return { matched: true, params };
  };
}

async function tryLoadModuleDefinition(): Promise<{ definition: ModuleDefinitionLike; source: string } | undefined> {
  const here = path.dirname(fileURLToPath(import.meta.url));
  const candidates = ['module.js', 'module.mjs', 'module/index.js', 'module/index.mjs'];
  for (const rel of candidates) {
    const full = path.join(here, rel);
    try {
      const imported = await import(`${pathToFileURL(full).href}?t=${Date.now()}`);
      const definition = extractModuleDefinition(imported);
      if (definition) {
        return { definition, source: rel };
      }
    } catch {
      // ignore and continue
    }
  }
  return undefined;
}

function extractModuleDefinition(exports: Record<string, unknown>): ModuleDefinitionLike | undefined {
  const keys = ['module', 'moduleDefinition', 'default', 'backendModule'];
  for (const key of keys) {
    if (key in exports) {
      const value = exports[key as keyof typeof exports];
      if (value && typeof value === 'object') {
        return value as ModuleDefinitionLike;
      }
    }
  }
  return undefined;
}

async function readRequestBody(req: http.IncomingMessage): Promise<unknown> {
  const method = (req.method ?? 'GET').toUpperCase();
  if (method === 'GET' || method === 'HEAD') {
    return undefined;
  }
  const chunks: Uint8Array[] = [];
  for await (const chunk of req) {
    chunks.push(typeof chunk === 'string' ? Buffer.from(chunk) : chunk);
  }
  if (chunks.length === 0) {
    return undefined;
  }
  const buffer = Buffer.concat(chunks);
  const contentType = String(req.headers['content-type'] ?? '');
  if (contentType.includes('application/json')) {
    try {
      return JSON.parse(buffer.toString('utf8'));
    } catch {
      return undefined;
    }
  }
  return buffer.toString('utf8');
}

function normalizePath(value: string | undefined): string {
  if (!value || value === '/') return '/';
  const trimmed = value.endsWith('/') ? value.slice(0, -1) : value;
  return trimmed.startsWith('/') ? trimmed : `/${trimmed}`;
}

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
