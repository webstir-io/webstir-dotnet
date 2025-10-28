# @webstir-io/module-contract

TypeScript interfaces, helper utilities, and JSON schema describing Webstir modules and providers. The contract covers build-time provider APIs and the runtime surface (contexts, manifests, routes, views) that providers expose to the orchestrator.

## Install

```ini
# .npmrc
@webstir-io:registry=https://npm.pkg.github.com
//npm.pkg.github.com/:_authToken=${GH_PACKAGES_TOKEN}
```

```bash
npm install @webstir-io/module-contract
```

Consumers only need `read:packages`; publishers also require `write:packages`.

## Provided Types

```ts
import {
  ModuleProvider,
  ModuleBuildResult,
  moduleManifestSchema,
  routeDefinitionSchema,
  viewDefinitionSchema,
  defineRoute,
  defineView,
  createModule,
  type RequestContext,
  type SSRContext,
} from '@webstir-io/module-contract';

// Optional adapters live under a secondary export for teams using ts-rest
import { fromTsRestRoute, fromTsRestRouter } from '@webstir-io/module-contract/ts-rest';
```

- `ModuleProvider` remains the build-time contract (`metadata`, `resolveWorkspace`, `build`).
- `moduleManifestSchema` / `routeDefinitionSchema` / `viewDefinitionSchema` model the runtime manifest that orchestrators ingest; the package publishes matching JSON schema under `schema/`.
- `defineRoute`, `defineView`, and `createModule` give providers ergonomic helpers with strong TypeScript inference.
- `RequestContext` and `SSRContext` describe what the orchestrator supplies to route and view handlers.
- `fromTsRestRoute` converts an `@ts-rest/core` route contract into a Webstir `RouteSpec`, and `fromTsRestRouter` adapts an entire ts-rest router tree at once.

> Install `@ts-rest/core` to use the adapters; it's published as an optional peer dependency of this package.

## Usage Example

```ts
import { createModule, defineRoute, defineView, RequestContext, SSRContext } from '@webstir-io/module-contract';
import { z } from 'zod';

const paramsSchema = z.object({ id: z.string().uuid() });
const responseSchema = z.object({
  id: z.string().uuid(),
  email: z.string().email()
});
const viewDataSchema = z.object({ account: responseSchema });

const getAccount = defineRoute<RequestContext, typeof paramsSchema, undefined, undefined, typeof responseSchema>({
  definition: {
    name: 'getAccount',
    method: 'GET',
    path: '/accounts/:id',
    input: { params: { kind: 'zod', name: 'AccountRouteParams' } },
    output: { body: { kind: 'zod', name: 'AccountRouteResponse' }, status: 200 }
  },
  schemas: {
    params: paramsSchema,
    response: responseSchema
  },
  handler: async (ctx) => ({
    status: 200,
    body: await ctx.db.accounts.findById(ctx.params.id)
  })
});

const accountView = defineView<SSRContext, typeof paramsSchema, typeof viewDataSchema>({
  definition: {
    name: 'AccountView',
    path: '/accounts/:id',
    params: { kind: 'zod', name: 'AccountViewParams' },
    data: { kind: 'zod', name: 'AccountViewData' }
  },
  params: paramsSchema,
  data: viewDataSchema,
  load: async (ctx) => ({ account: await ctx.env.api.fetchAccount(ctx.params.id) })
});

export const accountsModule = createModule({
  manifest: {
    contractVersion: '1.0.0',
    name: '@demo/accounts',
    version: '0.0.1',
    kind: 'backend',
    capabilities: ['auth', 'db', 'views'],
    routes: [getAccount.definition],
    views: [accountView.definition]
  },
  routes: [getAccount],
  views: [accountView]
});
```

### ts-rest Router Example

```ts
import { initContract } from '@ts-rest/core';
import { fromTsRestRouter, type RequestContext } from '@webstir-io/module-contract/ts-rest';
import { z } from 'zod';

const c = initContract();

const accountsRouter = c.router({
  list: c.query({
    path: '/accounts',
    method: 'GET',
    responses: {
      200: z.object({
        data: z.array(z.object({ id: z.string(), email: z.string().email() }))
      })
    }
  }),
  detail: c.query({
    path: '/accounts/:id',
    method: 'GET',
    pathParams: z.object({ id: z.string().uuid() }),
    responses: {
      200: z.object({ id: z.string().uuid(), email: z.string().email() }),
      404: z.null()
    }
  })
});

const routeSpecs = fromTsRestRouter<RequestContext>({
  router: accountsRouter,
  baseName: 'accounts',
  createRoute: ({ keyPath, appRoute }) => ({
    handler: async (ctx) => {
      if (keyPath.at(-1) === 'detail') {
        const account = await ctx.db.accounts.findById(ctx.params.id);
        return account
          ? { status: 200, body: account }
          : { status: 404, errors: [{ code: 'not_found', message: 'Account missing' }] };
      }

      const accounts = await ctx.db.accounts.list();
      return { status: 200, body: { data: accounts } };
    },
    successStatus: appRoute.method === 'GET' ? 200 : undefined
  })
});

// routeSpecs is a RouteSpec[] ready to feed into createModule({ routes: routeSpecs })
```

When authoring a provider:

1. Populate `metadata` with id, version, and CLI compatibility info.
2. Use `createModule`/`defineRoute`/`defineView` to declare runtime capabilities with Zod-powered validation.
3. Return absolute filesystem paths in `ModuleArtifact.path` from the build step.
4. Emit `ModuleDiagnostic`s for recoverable issues and include the module manifest in `ModuleBuildResult.manifest.module`.

## Maintainer Workflow

```bash
npm install
npm run build          # cleans, compiles TypeScript, regenerates schema/*.schema.json
npm run test           # type-checks the Accounts example module
# Release helper (bumps version/test/publish to GitHub Packages, prompts to push)
npm run release -- patch
```

- The `schema/` folder contains `*-definition.schema.json` files derived from the exported Zod schemas. Commit them with contract changes.
- Ensure CI runs `npm run build` and fails on schema drift before publish.

## License

MIT © Webstir
