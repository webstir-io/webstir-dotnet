import { ContractNoBody, isAppRoute, isAppRouteMutation, isAppRouteNoBody, isAppRouteOtherResponse, isZodType } from '@ts-rest/core';
import type { AppRoute, AppRouteResponse, AppRouter } from '@ts-rest/core';
import { z } from 'zod';

import type {
  ModuleError,
  RequestContext,
  RouteHandler,
  RouteSpec,
  SchemaReference
} from '../index.js';

interface SchemaNames {
  readonly params?: SchemaReference;
  readonly query?: SchemaReference;
  readonly body?: SchemaReference;
  readonly headers?: SchemaReference;
  readonly response?: SchemaReference;
}

type MutableSchemaNames = {
  params?: SchemaReference;
  query?: SchemaReference;
  body?: SchemaReference;
  headers?: SchemaReference;
  response?: SchemaReference;
};

interface BaseFromTsRestRouteOptions {
  readonly name: string;
  readonly appRoute: AppRoute;
  readonly summary?: string;
  readonly description?: string;
  readonly tags?: readonly string[];
  readonly schemaBaseName?: string;
  readonly successStatus?: number;
  readonly errors?: readonly ModuleError[];
  readonly errorSchemas?: readonly z.ZodTypeAny[];
}

export interface FromTsRestRouteOptions<
  TContext extends RequestContext,
  TParams extends z.ZodTypeAny | undefined,
  TQuery extends z.ZodTypeAny | undefined,
  TBody extends z.ZodTypeAny | undefined,
  TResponse extends z.ZodTypeAny
> extends BaseFromTsRestRouteOptions {
  readonly handler: RouteHandler<TContext, TParams, TQuery, TBody, TResponse>;
  readonly paramsSchema?: TParams;
  readonly querySchema?: TQuery;
  readonly bodySchema?: TBody;
  readonly headersSchema?: z.ZodTypeAny;
  readonly responseSchema?: TResponse;
}

type AnyRouteOptions<TContext extends RequestContext> = FromTsRestRouteOptions<
  TContext,
  z.ZodTypeAny | undefined,
  z.ZodTypeAny | undefined,
  z.ZodTypeAny | undefined,
  z.ZodTypeAny
>;

export interface RouterRouteConfig<TContext extends RequestContext> extends Omit<AnyRouteOptions<TContext>, 'appRoute' | 'name'> {
  readonly name?: string;
}

export interface FromTsRestRouterOptions<TContext extends RequestContext> {
  readonly router: AppRouter;
  readonly baseName?: string;
  readonly createRoute: (info: {
    readonly key: string;
    readonly keyPath: readonly string[];
    readonly appRoute: AppRoute;
  }) => RouterRouteConfig<TContext>;
}

const DEFAULT_SUCCESS_CODES = [200, 201, 202, 204];

const noopResponseSchema = z.void();

const isNonEmpty = <T>(value: readonly T[] | undefined): value is readonly T[] =>
  Array.isArray(value) && value.length > 0;

const sanitizeSchemaBase = (raw: string): string => raw.replace(/[^A-Za-z0-9]+/g, '_');

const capitalize = (value: string): string =>
  value.length === 0 ? value : value[0].toUpperCase() + value.slice(1);

const buildSchemaNames = (baseName: string, schemas: {
  readonly params?: z.ZodTypeAny;
  readonly query?: z.ZodTypeAny;
  readonly body?: z.ZodTypeAny;
  readonly headers?: z.ZodTypeAny;
  readonly response?: z.ZodTypeAny;
}): SchemaNames => {
  const names: MutableSchemaNames = {};
  const safeBase = sanitizeSchemaBase(baseName);

  if (schemas.params) {
    names.params = { kind: 'zod', name: `${safeBase}${capitalize('params')}` };
  }
  if (schemas.query) {
    names.query = { kind: 'zod', name: `${safeBase}${capitalize('query')}` };
  }
  if (schemas.body) {
    names.body = { kind: 'zod', name: `${safeBase}${capitalize('body')}` };
  }
  if (schemas.headers) {
    names.headers = { kind: 'zod', name: `${safeBase}${capitalize('headers')}` };
  }
  if (schemas.response) {
    names.response = { kind: 'zod', name: `${safeBase}${capitalize('response')}` };
  }

  return names;
};

const selectResponse = (route: AppRoute, preferred?: number): { status?: number; response?: AppRouteResponse } => {
  const statusCodes = Object.keys(route.responses).map((status) => Number.parseInt(status, 10));
  if (statusCodes.length === 0) {
    return { response: undefined };
  }

  const ordered = statusCodes.sort((a, b) => a - b);
  const preferredStatus = preferred ?? ordered.find((code) => DEFAULT_SUCCESS_CODES.includes(code));
  const status = preferredStatus ?? ordered[0];
  return { status, response: route.responses[status] };
};

const toZod = (value: unknown): z.ZodTypeAny | undefined => {
  if (!value) {
    return undefined;
  }

  if (isZodType(value)) {
    return value as z.ZodTypeAny;
  }

  if (typeof value === 'object' && value !== null && 'body' in (value as Record<string, unknown>)) {
    const body = (value as { body?: unknown }).body;
    if (isZodType(body)) {
      return body as z.ZodTypeAny;
    }
  }

  return undefined;
};

const extractResponseSchema = (response: AppRouteResponse | undefined): z.ZodTypeAny | undefined => {
  if (!response) {
    return undefined;
  }
  if (response === ContractNoBody || isAppRouteNoBody(response)) {
    return undefined;
  }
  if (isAppRouteOtherResponse(response)) {
    return toZod(response.body);
  }
  return toZod(response);
};

export function fromTsRestRoute<
  TContext extends RequestContext,
  TParams extends z.ZodTypeAny | undefined = undefined,
  TQuery extends z.ZodTypeAny | undefined = undefined,
  TBody extends z.ZodTypeAny | undefined = undefined,
  TResponse extends z.ZodTypeAny = z.ZodTypeAny
>(
  options: FromTsRestRouteOptions<TContext, TParams, TQuery, TBody, TResponse>
): RouteSpec<TContext, TParams, TQuery, TBody, TResponse> {
  const {
    appRoute,
    name,
    summary,
    description,
    tags,
    schemaBaseName,
    successStatus,
    paramsSchema,
    querySchema,
    bodySchema,
    headersSchema,
    responseSchema,
    handler,
    errors,
    errorSchemas
  } = options;

  const resolvedParamsSchema = (paramsSchema ?? toZod(appRoute.pathParams)) as TParams;
  const resolvedQuerySchema = (querySchema ?? toZod(appRoute.query)) as TQuery;
  const resolvedHeadersSchema = headersSchema ?? toZod(appRoute.headers);
  const mutationBodySchema = isAppRouteMutation(appRoute) ? toZod(appRoute.body) : undefined;
  const resolvedBodySchema = (bodySchema ?? mutationBodySchema) as TBody;

  const { status: derivedStatus, response: routeResponse } = selectResponse(appRoute, successStatus);
  const baseResponseSchema = extractResponseSchema(routeResponse);
  const resolvedResponseSchema = (responseSchema ?? baseResponseSchema ?? noopResponseSchema) as TResponse;

  const schemaNames = buildSchemaNames(schemaBaseName ?? name, {
    params: resolvedParamsSchema,
    query: resolvedQuerySchema,
    body: resolvedBodySchema,
    headers: resolvedHeadersSchema,
    response: resolvedResponseSchema
  });

  const definitionInput: Record<string, SchemaReference | undefined> = {
    params: schemaNames.params,
    query: schemaNames.query,
    body: schemaNames.body,
    headers: schemaNames.headers
  };

  const hasInput = Object.values(definitionInput).some((value) => value !== undefined);

  return {
    definition: {
      name,
      method: appRoute.method,
      path: appRoute.path,
      summary: summary ?? appRoute.summary,
      description: description ?? appRoute.description,
      tags: isNonEmpty(tags) ? [...tags] : undefined,
      input: hasInput
        ? {
            params: schemaNames.params,
            query: schemaNames.query,
            body: schemaNames.body,
            headers: schemaNames.headers
          }
        : undefined,
      output: schemaNames.response
        ? {
            body: schemaNames.response,
            status: derivedStatus
          }
        : undefined,
      errors: isNonEmpty(errors) ? [...errors] : undefined
    },
    schemas: {
      params: resolvedParamsSchema,
      query: resolvedQuerySchema,
      body: resolvedBodySchema,
      headers: resolvedHeadersSchema,
      response: resolvedResponseSchema,
      errors: errorSchemas
    },
    handler
  };
}

export function fromTsRestRouter<TContext extends RequestContext>(
  options: FromTsRestRouterOptions<TContext>
): RouteSpec<TContext, any, any, any, any>[] {
  const { router, createRoute, baseName } = options;
  const specs: RouteSpec<TContext, any, any, any, any>[] = [];

  const visit = (node: AppRouter, path: readonly string[]) => {
    for (const [key, value] of Object.entries(node)) {
      if (isAppRoute(value)) {
        const keyPath = [...path, key];
        const routeConfig = createRoute({ key, keyPath, appRoute: value });
        const nameSegments = [baseName, ...keyPath.filter(Boolean)].filter(Boolean) as string[];
        const routeName = routeConfig.name ?? nameSegments.join('.');

        specs.push(
          fromTsRestRoute({
            appRoute: value,
            name: routeName,
            ...routeConfig
          })
        );
      } else {
        visit(value as AppRouter, [...path, key]);
      }
    }
  };

  visit(router, []);
  return specs;
}
