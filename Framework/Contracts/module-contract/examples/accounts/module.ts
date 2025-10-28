import { z } from 'zod';

import {
  createModule,
  defineRoute,
  defineView,
  type RequestContext,
  type SSRContext
} from '@webstir-io/module-contract';

const accountParamsSchema = z.object({
  id: z.string().uuid()
});

const accountResponseSchema = z.object({
  id: z.string().uuid(),
  email: z.string().email(),
  createdAt: z.string()
});

const accountViewDataSchema = z.object({
  account: accountResponseSchema
});

const getAccountRoute = defineRoute<RequestContext, typeof accountParamsSchema, undefined, undefined, typeof accountResponseSchema>({
  definition: {
    name: 'getAccount',
    method: 'GET',
    path: '/accounts/:id',
    summary: 'Fetch an account by id',
    input: {
      params: { kind: 'zod', name: 'AccountRouteParams' }
    },
    output: {
      body: { kind: 'zod', name: 'AccountRouteResponse' },
      status: 200
    }
  },
  schemas: {
    params: accountParamsSchema,
    response: accountResponseSchema
  },
  handler: async (ctx) => {
    ctx.logger.debug('load-account', { id: ctx.params.id });
    return {
      status: 200,
      body: {
        id: ctx.params.id,
        email: 'demo@example.com',
        createdAt: new Date().toISOString()
      }
    };
  }
});

const accountView = defineView<SSRContext, typeof accountParamsSchema, typeof accountViewDataSchema>({
  definition: {
    name: 'AccountView',
    path: '/accounts/:id',
    summary: 'SSR view that loads account details',
    params: { kind: 'zod', name: 'AccountViewParams' },
    data: { kind: 'zod', name: 'AccountViewData' }
  },
  params: accountParamsSchema,
  data: accountViewDataSchema,
  load: async (ctx) => {
    ctx.logger.info('render-account-view', { id: ctx.params.id });
    return {
      account: {
        id: ctx.params.id,
        email: 'demo@example.com',
        createdAt: new Date().toISOString()
      }
    };
  }
});

export const accountsModule = createModule({
  manifest: {
    contractVersion: '1.0.0',
    name: '@demo/accounts',
    version: '0.0.1',
    kind: 'backend',
    capabilities: ['auth', 'db', 'views'],
    routes: [getAccountRoute.definition],
    views: [accountView.definition]
  },
  routes: [getAccountRoute],
  views: [accountView],
  init: async ({ logger }) => {
    logger.info('accounts module init');
  },
  dispose: async ({ logger }) => {
    logger.info('accounts module dispose');
  }
});

// Ensure inference works as expected.
type LoadedAccount = Awaited<ReturnType<typeof accountView.load>>;
const _: LoadedAccount = {
  account: {
    id: '00000000-0000-0000-0000-000000000000',
    email: 'demo@example.com',
    createdAt: new Date().toISOString()
  }
};

void _;
