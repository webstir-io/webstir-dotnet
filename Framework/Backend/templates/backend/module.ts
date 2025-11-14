// Example manifest + route definition. Update the values to match your backend.
// When this file is present, the server scaffold loads build/backend/module.js,
// announces the manifest, and mounts every route definition automatically.

interface RouteHandlerResult {
  status?: number;
  headers?: Record<string, string>;
  body?: unknown;
  errors?: { code: string; message: string; details?: unknown }[];
}

type RouteHandler = (ctx: RouteContext) => Promise<RouteHandlerResult> | RouteHandlerResult;

interface RouteContext {
  params: Record<string, string>;
  query: Record<string, string>;
  body: unknown;
  auth?: {
    userId?: string;
    email?: string;
    scopes: readonly string[];
    roles: readonly string[];
  };
  requestId: string;
  env: {
    get: (name: string) => string | undefined;
    require: (name: string) => string;
    entries: () => Record<string, string | undefined>;
  };
  logger: {
    info: (message: string, metadata?: Record<string, unknown>) => void;
    warn: (message: string, metadata?: Record<string, unknown>) => void;
    error: (message: string, metadata?: Record<string, unknown>) => void;
  };
  now: () => Date;
}

const routes = [
  {
    definition: {
      name: 'helloRoute',
      method: 'GET',
      path: '/hello/:name',
      summary: 'Simple hello route',
      description: 'Demonstrates manifest wiring + request context metadata.'
    },
    handler: async (ctx: RouteContext) => {
      if (!ctx.auth) {
        return {
          status: 401,
          errors: [{ code: 'auth', message: 'Sign-in required to access /hello' }]
        };
      }
      const name = ctx.params.name ?? 'world';
      ctx.logger.info('hello route invoked', { name, requestId: ctx.requestId, userId: ctx.auth.userId });
      return {
        status: 200,
        body: {
          message: `Hello ${name}`,
          greetedAt: ctx.now().toISOString(),
          user: ctx.auth.userId ?? 'anonymous'
        }
      };
    }
  }
];

const jobs = [
  {
    name: 'nightly',
    schedule: '0 0 * * *',
    description: 'Example nightly maintenance job metadata surfaced in the manifest.'
  }
];

export const module = {
  manifest: {
    contractVersion: '1.0.0',
    name: '@demo/backend',
    version: '0.1.0',
    kind: 'backend',
    capabilities: ['http', 'auth', 'db'],
    routes: routes.map((route) => route.definition),
    jobs
  },
  routes
};
