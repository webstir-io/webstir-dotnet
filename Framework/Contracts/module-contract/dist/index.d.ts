import { z } from 'zod';
export declare const CONTRACT_VERSION: "1.0.0";
export declare const contractVersionLiteral: z.ZodLiteral<"1.0.0">;
export type ModuleKind = 'frontend' | 'backend';
export interface ModuleCompatibility {
    readonly minCliVersion: string;
    readonly maxCliVersion?: string;
    readonly nodeRange: string;
    readonly notes?: string;
}
export interface ModuleProviderMetadata {
    readonly id: string;
    readonly kind: ModuleKind;
    readonly version: string;
    readonly compatibility: ModuleCompatibility;
}
export interface ResolveWorkspaceOptions {
    readonly workspaceRoot: string;
    readonly config: Record<string, unknown>;
}
export interface ResolvedModuleWorkspace {
    readonly sourceRoot: string;
    readonly buildRoot: string;
    readonly testsRoot?: string;
}
export interface ModuleBuildOptions {
    readonly workspaceRoot: string;
    readonly env: Record<string, string | undefined>;
    readonly incremental?: boolean;
}
export interface ModuleDiagnostic {
    readonly severity: 'info' | 'warn' | 'error';
    readonly message: string;
    readonly file?: string;
}
export interface ModuleBuildManifest {
    readonly entryPoints: readonly string[];
    readonly staticAssets: readonly string[];
    readonly diagnostics: readonly ModuleDiagnostic[];
    readonly module?: ModuleManifest;
}
export interface ModuleArtifact {
    readonly path: string;
    readonly type: 'asset' | 'bundle' | 'metadata';
}
export interface ModuleBuildResult {
    readonly artifacts: readonly ModuleArtifact[];
    readonly manifest: ModuleBuildManifest;
}
export interface ModuleAsset {
    readonly sourcePath: string;
    readonly targetPath: string;
}
export interface ModuleProvider {
    readonly metadata: ModuleProviderMetadata;
    resolveWorkspace(options: ResolveWorkspaceOptions): Promise<ResolvedModuleWorkspace> | ResolvedModuleWorkspace;
    build(options: ModuleBuildOptions): Promise<ModuleBuildResult> | ModuleBuildResult;
    getScaffoldAssets?(): Promise<readonly ModuleAsset[]> | readonly ModuleAsset[];
}
export declare const moduleKindSchema: z.ZodEnum<["frontend", "backend"]>;
export type LogLevel = 'trace' | 'debug' | 'info' | 'warn' | 'error' | 'fatal';
export declare const logLevelSchema: z.ZodEnum<["trace", "debug", "info", "warn", "error", "fatal"]>;
export interface Logger {
    readonly level: LogLevel;
    log(level: LogLevel, message: string, metadata?: Record<string, unknown>): void;
    debug(message: string, metadata?: Record<string, unknown>): void;
    info(message: string, metadata?: Record<string, unknown>): void;
    warn(message: string, metadata?: Record<string, unknown>): void;
    error(message: string, metadata?: Record<string, unknown>): void;
    with(bindings: Record<string, unknown>): Logger;
}
export interface EnvAccessor {
    get(name: string): string | undefined;
    require(name: string): string;
    entries(): Record<string, string | undefined>;
}
export interface RequestContext<TRequest = unknown, TReply = unknown, TAuth = unknown, TSession = unknown, TDatabase = unknown, TEnv extends EnvAccessor = EnvAccessor, TLogger extends Logger = Logger> {
    readonly request: TRequest;
    readonly reply: TReply;
    readonly auth: TAuth;
    readonly session: TSession | null;
    readonly db: TDatabase;
    readonly env: TEnv;
    readonly logger: TLogger;
    readonly requestId: string;
    readonly now: () => Date;
}
export interface SSRContext<TParams = Record<string, string>, TAuth = unknown, TSession = unknown, TEnv extends EnvAccessor = EnvAccessor, TLogger extends Logger = Logger> {
    readonly url: URL;
    readonly params: TParams;
    readonly cookies: Record<string, string>;
    readonly headers: Record<string, string>;
    readonly auth: TAuth;
    readonly session: TSession | null;
    readonly env: TEnv;
    readonly logger: TLogger;
    readonly requestId?: string;
    readonly now: () => Date;
}
export interface AuthSession<TData = Record<string, unknown>> {
    readonly id: string;
    readonly userId?: string;
    readonly data: TData;
    readonly createdAt: Date;
    readonly expiresAt?: Date;
}
export interface CreateSessionInput<TData = Record<string, unknown>> {
    readonly userId?: string;
    readonly data: TData;
    readonly expiresInSeconds?: number;
}
export type PermissionCheckResult = {
    readonly allowed: true;
} | {
    readonly allowed: false;
    readonly reason?: string;
};
export interface AuthProvider<TSession extends AuthSession = AuthSession> {
    getSession(context: RequestContext | SSRContext): Promise<TSession | null> | TSession | null;
    createSession(input: CreateSessionInput<TSession['data']>, context: RequestContext | SSRContext): Promise<TSession> | TSession;
    invalidateSession(sessionId: string, context: RequestContext | SSRContext): Promise<void> | void;
    getCsrfToken?(context: RequestContext | SSRContext): Promise<string> | string;
    verifyPermissions?(context: RequestContext | SSRContext, permissions: readonly string[]): Promise<PermissionCheckResult> | PermissionCheckResult;
}
export interface DatabaseTransaction<TDatabase = unknown> {
    readonly run: <TResult>(callback: (client: TDatabase) => Promise<TResult> | TResult) => Promise<TResult>;
}
export interface DatabaseProvider<TDatabase = unknown> {
    readonly client: TDatabase;
    transaction?(): DatabaseTransaction<TDatabase>;
}
export interface CacheProvider<TKey = string, TValue = unknown> {
    get(key: TKey): Promise<TValue | undefined> | TValue | undefined;
    set(key: TKey, value: TValue, options?: {
        readonly ttlSeconds?: number;
    }): Promise<void> | void;
    delete(key: TKey): Promise<void> | void;
}
export interface QueueMessage<TPayload = unknown> {
    readonly id: string;
    readonly payload: TPayload;
    readonly attempts: number;
    readonly enqueuedAt: Date;
}
export interface QueueProvider<TPayload = unknown> {
    enqueue(payload: TPayload, options?: {
        readonly delaySeconds?: number;
    }): Promise<string> | string;
    process(handler: (message: QueueMessage<TPayload>) => Promise<void> | void): Promise<void> | void;
}
export interface TestingManifest {
    readonly name: string;
    readonly version: string;
    readonly capabilities: readonly string[];
}
export interface TestingProvider {
    readonly manifest: TestingManifest;
    run(options: {
        readonly workspaceRoot: string;
        readonly env: Record<string, string | undefined>;
    }): Promise<void> | void;
}
export declare const moduleErrorCodeSchema: z.ZodEnum<["validation", "auth", "not_found", "domain", "conflict", "internal"]>;
export type ModuleErrorCode = z.infer<typeof moduleErrorCodeSchema>;
export declare const moduleErrorSchema: z.ZodObject<{
    code: z.ZodEnum<["validation", "auth", "not_found", "domain", "conflict", "internal"]>;
    message: z.ZodString;
    details: z.ZodOptional<z.ZodUnknown>;
    cause: z.ZodOptional<z.ZodUnknown>;
    correlationId: z.ZodOptional<z.ZodString>;
}, "strip", z.ZodTypeAny, {
    code: "validation" | "auth" | "not_found" | "domain" | "conflict" | "internal";
    message: string;
    details?: unknown;
    cause?: unknown;
    correlationId?: string | undefined;
}, {
    code: "validation" | "auth" | "not_found" | "domain" | "conflict" | "internal";
    message: string;
    details?: unknown;
    cause?: unknown;
    correlationId?: string | undefined;
}>;
export type ModuleError = z.infer<typeof moduleErrorSchema>;
export declare const httpMethodSchema: z.ZodEnum<["GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"]>;
export type HttpMethod = z.infer<typeof httpMethodSchema>;
export declare const schemaReferenceSchema: z.ZodObject<{
    kind: z.ZodDefault<z.ZodEnum<["zod", "json-schema", "ts-rest"]>>;
    name: z.ZodString;
    source: z.ZodOptional<z.ZodString>;
}, "strip", z.ZodTypeAny, {
    kind: "zod" | "json-schema" | "ts-rest";
    name: string;
    source?: string | undefined;
}, {
    name: string;
    kind?: "zod" | "json-schema" | "ts-rest" | undefined;
    source?: string | undefined;
}>;
export type SchemaReference = z.infer<typeof schemaReferenceSchema>;
export declare const routeInputSchema: z.ZodObject<{
    params: z.ZodOptional<z.ZodOptional<z.ZodObject<{
        kind: z.ZodDefault<z.ZodEnum<["zod", "json-schema", "ts-rest"]>>;
        name: z.ZodString;
        source: z.ZodOptional<z.ZodString>;
    }, "strip", z.ZodTypeAny, {
        kind: "zod" | "json-schema" | "ts-rest";
        name: string;
        source?: string | undefined;
    }, {
        name: string;
        kind?: "zod" | "json-schema" | "ts-rest" | undefined;
        source?: string | undefined;
    }>>>;
    query: z.ZodOptional<z.ZodOptional<z.ZodObject<{
        kind: z.ZodDefault<z.ZodEnum<["zod", "json-schema", "ts-rest"]>>;
        name: z.ZodString;
        source: z.ZodOptional<z.ZodString>;
    }, "strip", z.ZodTypeAny, {
        kind: "zod" | "json-schema" | "ts-rest";
        name: string;
        source?: string | undefined;
    }, {
        name: string;
        kind?: "zod" | "json-schema" | "ts-rest" | undefined;
        source?: string | undefined;
    }>>>;
    body: z.ZodOptional<z.ZodOptional<z.ZodObject<{
        kind: z.ZodDefault<z.ZodEnum<["zod", "json-schema", "ts-rest"]>>;
        name: z.ZodString;
        source: z.ZodOptional<z.ZodString>;
    }, "strip", z.ZodTypeAny, {
        kind: "zod" | "json-schema" | "ts-rest";
        name: string;
        source?: string | undefined;
    }, {
        name: string;
        kind?: "zod" | "json-schema" | "ts-rest" | undefined;
        source?: string | undefined;
    }>>>;
    headers: z.ZodOptional<z.ZodOptional<z.ZodObject<{
        kind: z.ZodDefault<z.ZodEnum<["zod", "json-schema", "ts-rest"]>>;
        name: z.ZodString;
        source: z.ZodOptional<z.ZodString>;
    }, "strip", z.ZodTypeAny, {
        kind: "zod" | "json-schema" | "ts-rest";
        name: string;
        source?: string | undefined;
    }, {
        name: string;
        kind?: "zod" | "json-schema" | "ts-rest" | undefined;
        source?: string | undefined;
    }>>>;
}, "strict", z.ZodTypeAny, {
    params?: {
        kind: "zod" | "json-schema" | "ts-rest";
        name: string;
        source?: string | undefined;
    } | undefined;
    query?: {
        kind: "zod" | "json-schema" | "ts-rest";
        name: string;
        source?: string | undefined;
    } | undefined;
    body?: {
        kind: "zod" | "json-schema" | "ts-rest";
        name: string;
        source?: string | undefined;
    } | undefined;
    headers?: {
        kind: "zod" | "json-schema" | "ts-rest";
        name: string;
        source?: string | undefined;
    } | undefined;
}, {
    params?: {
        name: string;
        kind?: "zod" | "json-schema" | "ts-rest" | undefined;
        source?: string | undefined;
    } | undefined;
    query?: {
        name: string;
        kind?: "zod" | "json-schema" | "ts-rest" | undefined;
        source?: string | undefined;
    } | undefined;
    body?: {
        name: string;
        kind?: "zod" | "json-schema" | "ts-rest" | undefined;
        source?: string | undefined;
    } | undefined;
    headers?: {
        name: string;
        kind?: "zod" | "json-schema" | "ts-rest" | undefined;
        source?: string | undefined;
    } | undefined;
}>;
export type RouteInputDefinition = z.infer<typeof routeInputSchema>;
export declare const routeOutputSchema: z.ZodObject<{
    body: z.ZodObject<{
        kind: z.ZodDefault<z.ZodEnum<["zod", "json-schema", "ts-rest"]>>;
        name: z.ZodString;
        source: z.ZodOptional<z.ZodString>;
    }, "strip", z.ZodTypeAny, {
        kind: "zod" | "json-schema" | "ts-rest";
        name: string;
        source?: string | undefined;
    }, {
        name: string;
        kind?: "zod" | "json-schema" | "ts-rest" | undefined;
        source?: string | undefined;
    }>;
    status: z.ZodOptional<z.ZodNumber>;
    headers: z.ZodOptional<z.ZodObject<{
        kind: z.ZodDefault<z.ZodEnum<["zod", "json-schema", "ts-rest"]>>;
        name: z.ZodString;
        source: z.ZodOptional<z.ZodString>;
    }, "strip", z.ZodTypeAny, {
        kind: "zod" | "json-schema" | "ts-rest";
        name: string;
        source?: string | undefined;
    }, {
        name: string;
        kind?: "zod" | "json-schema" | "ts-rest" | undefined;
        source?: string | undefined;
    }>>;
}, "strip", z.ZodTypeAny, {
    body: {
        kind: "zod" | "json-schema" | "ts-rest";
        name: string;
        source?: string | undefined;
    };
    status?: number | undefined;
    headers?: {
        kind: "zod" | "json-schema" | "ts-rest";
        name: string;
        source?: string | undefined;
    } | undefined;
}, {
    body: {
        name: string;
        kind?: "zod" | "json-schema" | "ts-rest" | undefined;
        source?: string | undefined;
    };
    status?: number | undefined;
    headers?: {
        name: string;
        kind?: "zod" | "json-schema" | "ts-rest" | undefined;
        source?: string | undefined;
    } | undefined;
}>;
export type RouteOutputDefinition = z.infer<typeof routeOutputSchema>;
export declare const routeDefinitionSchema: z.ZodObject<{
    name: z.ZodString;
    method: z.ZodEnum<["GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"]>;
    path: z.ZodString;
    summary: z.ZodOptional<z.ZodString>;
    description: z.ZodOptional<z.ZodString>;
    tags: z.ZodOptional<z.ZodArray<z.ZodString, "many">>;
    input: z.ZodOptional<z.ZodObject<{
        params: z.ZodOptional<z.ZodOptional<z.ZodObject<{
            kind: z.ZodDefault<z.ZodEnum<["zod", "json-schema", "ts-rest"]>>;
            name: z.ZodString;
            source: z.ZodOptional<z.ZodString>;
        }, "strip", z.ZodTypeAny, {
            kind: "zod" | "json-schema" | "ts-rest";
            name: string;
            source?: string | undefined;
        }, {
            name: string;
            kind?: "zod" | "json-schema" | "ts-rest" | undefined;
            source?: string | undefined;
        }>>>;
        query: z.ZodOptional<z.ZodOptional<z.ZodObject<{
            kind: z.ZodDefault<z.ZodEnum<["zod", "json-schema", "ts-rest"]>>;
            name: z.ZodString;
            source: z.ZodOptional<z.ZodString>;
        }, "strip", z.ZodTypeAny, {
            kind: "zod" | "json-schema" | "ts-rest";
            name: string;
            source?: string | undefined;
        }, {
            name: string;
            kind?: "zod" | "json-schema" | "ts-rest" | undefined;
            source?: string | undefined;
        }>>>;
        body: z.ZodOptional<z.ZodOptional<z.ZodObject<{
            kind: z.ZodDefault<z.ZodEnum<["zod", "json-schema", "ts-rest"]>>;
            name: z.ZodString;
            source: z.ZodOptional<z.ZodString>;
        }, "strip", z.ZodTypeAny, {
            kind: "zod" | "json-schema" | "ts-rest";
            name: string;
            source?: string | undefined;
        }, {
            name: string;
            kind?: "zod" | "json-schema" | "ts-rest" | undefined;
            source?: string | undefined;
        }>>>;
        headers: z.ZodOptional<z.ZodOptional<z.ZodObject<{
            kind: z.ZodDefault<z.ZodEnum<["zod", "json-schema", "ts-rest"]>>;
            name: z.ZodString;
            source: z.ZodOptional<z.ZodString>;
        }, "strip", z.ZodTypeAny, {
            kind: "zod" | "json-schema" | "ts-rest";
            name: string;
            source?: string | undefined;
        }, {
            name: string;
            kind?: "zod" | "json-schema" | "ts-rest" | undefined;
            source?: string | undefined;
        }>>>;
    }, "strict", z.ZodTypeAny, {
        params?: {
            kind: "zod" | "json-schema" | "ts-rest";
            name: string;
            source?: string | undefined;
        } | undefined;
        query?: {
            kind: "zod" | "json-schema" | "ts-rest";
            name: string;
            source?: string | undefined;
        } | undefined;
        body?: {
            kind: "zod" | "json-schema" | "ts-rest";
            name: string;
            source?: string | undefined;
        } | undefined;
        headers?: {
            kind: "zod" | "json-schema" | "ts-rest";
            name: string;
            source?: string | undefined;
        } | undefined;
    }, {
        params?: {
            name: string;
            kind?: "zod" | "json-schema" | "ts-rest" | undefined;
            source?: string | undefined;
        } | undefined;
        query?: {
            name: string;
            kind?: "zod" | "json-schema" | "ts-rest" | undefined;
            source?: string | undefined;
        } | undefined;
        body?: {
            name: string;
            kind?: "zod" | "json-schema" | "ts-rest" | undefined;
            source?: string | undefined;
        } | undefined;
        headers?: {
            name: string;
            kind?: "zod" | "json-schema" | "ts-rest" | undefined;
            source?: string | undefined;
        } | undefined;
    }>>;
    output: z.ZodOptional<z.ZodObject<{
        body: z.ZodObject<{
            kind: z.ZodDefault<z.ZodEnum<["zod", "json-schema", "ts-rest"]>>;
            name: z.ZodString;
            source: z.ZodOptional<z.ZodString>;
        }, "strip", z.ZodTypeAny, {
            kind: "zod" | "json-schema" | "ts-rest";
            name: string;
            source?: string | undefined;
        }, {
            name: string;
            kind?: "zod" | "json-schema" | "ts-rest" | undefined;
            source?: string | undefined;
        }>;
        status: z.ZodOptional<z.ZodNumber>;
        headers: z.ZodOptional<z.ZodObject<{
            kind: z.ZodDefault<z.ZodEnum<["zod", "json-schema", "ts-rest"]>>;
            name: z.ZodString;
            source: z.ZodOptional<z.ZodString>;
        }, "strip", z.ZodTypeAny, {
            kind: "zod" | "json-schema" | "ts-rest";
            name: string;
            source?: string | undefined;
        }, {
            name: string;
            kind?: "zod" | "json-schema" | "ts-rest" | undefined;
            source?: string | undefined;
        }>>;
    }, "strip", z.ZodTypeAny, {
        body: {
            kind: "zod" | "json-schema" | "ts-rest";
            name: string;
            source?: string | undefined;
        };
        status?: number | undefined;
        headers?: {
            kind: "zod" | "json-schema" | "ts-rest";
            name: string;
            source?: string | undefined;
        } | undefined;
    }, {
        body: {
            name: string;
            kind?: "zod" | "json-schema" | "ts-rest" | undefined;
            source?: string | undefined;
        };
        status?: number | undefined;
        headers?: {
            name: string;
            kind?: "zod" | "json-schema" | "ts-rest" | undefined;
            source?: string | undefined;
        } | undefined;
    }>>;
    errors: z.ZodOptional<z.ZodArray<z.ZodObject<{
        code: z.ZodEnum<["validation", "auth", "not_found", "domain", "conflict", "internal"]>;
        message: z.ZodString;
        details: z.ZodOptional<z.ZodUnknown>;
        cause: z.ZodOptional<z.ZodUnknown>;
        correlationId: z.ZodOptional<z.ZodString>;
    }, "strip", z.ZodTypeAny, {
        code: "validation" | "auth" | "not_found" | "domain" | "conflict" | "internal";
        message: string;
        details?: unknown;
        cause?: unknown;
        correlationId?: string | undefined;
    }, {
        code: "validation" | "auth" | "not_found" | "domain" | "conflict" | "internal";
        message: string;
        details?: unknown;
        cause?: unknown;
        correlationId?: string | undefined;
    }>, "many">>;
}, "strip", z.ZodTypeAny, {
    name: string;
    path: string;
    method: "GET" | "POST" | "DELETE" | "PUT" | "PATCH" | "HEAD" | "OPTIONS";
    summary?: string | undefined;
    description?: string | undefined;
    tags?: string[] | undefined;
    errors?: {
        code: "validation" | "auth" | "not_found" | "domain" | "conflict" | "internal";
        message: string;
        details?: unknown;
        cause?: unknown;
        correlationId?: string | undefined;
    }[] | undefined;
    input?: {
        params?: {
            kind: "zod" | "json-schema" | "ts-rest";
            name: string;
            source?: string | undefined;
        } | undefined;
        query?: {
            kind: "zod" | "json-schema" | "ts-rest";
            name: string;
            source?: string | undefined;
        } | undefined;
        body?: {
            kind: "zod" | "json-schema" | "ts-rest";
            name: string;
            source?: string | undefined;
        } | undefined;
        headers?: {
            kind: "zod" | "json-schema" | "ts-rest";
            name: string;
            source?: string | undefined;
        } | undefined;
    } | undefined;
    output?: {
        body: {
            kind: "zod" | "json-schema" | "ts-rest";
            name: string;
            source?: string | undefined;
        };
        status?: number | undefined;
        headers?: {
            kind: "zod" | "json-schema" | "ts-rest";
            name: string;
            source?: string | undefined;
        } | undefined;
    } | undefined;
}, {
    name: string;
    path: string;
    method: "GET" | "POST" | "DELETE" | "PUT" | "PATCH" | "HEAD" | "OPTIONS";
    summary?: string | undefined;
    description?: string | undefined;
    tags?: string[] | undefined;
    errors?: {
        code: "validation" | "auth" | "not_found" | "domain" | "conflict" | "internal";
        message: string;
        details?: unknown;
        cause?: unknown;
        correlationId?: string | undefined;
    }[] | undefined;
    input?: {
        params?: {
            name: string;
            kind?: "zod" | "json-schema" | "ts-rest" | undefined;
            source?: string | undefined;
        } | undefined;
        query?: {
            name: string;
            kind?: "zod" | "json-schema" | "ts-rest" | undefined;
            source?: string | undefined;
        } | undefined;
        body?: {
            name: string;
            kind?: "zod" | "json-schema" | "ts-rest" | undefined;
            source?: string | undefined;
        } | undefined;
        headers?: {
            name: string;
            kind?: "zod" | "json-schema" | "ts-rest" | undefined;
            source?: string | undefined;
        } | undefined;
    } | undefined;
    output?: {
        body: {
            name: string;
            kind?: "zod" | "json-schema" | "ts-rest" | undefined;
            source?: string | undefined;
        };
        status?: number | undefined;
        headers?: {
            name: string;
            kind?: "zod" | "json-schema" | "ts-rest" | undefined;
            source?: string | undefined;
        } | undefined;
    } | undefined;
}>;
export type RouteDefinition = z.infer<typeof routeDefinitionSchema>;
export type InferOrNever<TSchema extends z.ZodTypeAny | undefined> = TSchema extends z.ZodTypeAny ? z.infer<TSchema> : Record<string, never>;
export interface RouteSchemas<TParams extends z.ZodTypeAny | undefined, TQuery extends z.ZodTypeAny | undefined, TBody extends z.ZodTypeAny | undefined, TResponse extends z.ZodTypeAny> {
    readonly params?: TParams;
    readonly query?: TQuery;
    readonly body?: TBody;
    readonly headers?: z.ZodTypeAny;
    readonly response: TResponse;
    readonly errors?: readonly z.ZodTypeAny[];
}
export type RouteHandlerContext<TContext extends RequestContext, TParams extends z.ZodTypeAny | undefined, TQuery extends z.ZodTypeAny | undefined, TBody extends z.ZodTypeAny | undefined> = TContext & {
    readonly params: InferOrNever<TParams>;
    readonly query: InferOrNever<TQuery>;
    readonly body: InferOrNever<TBody>;
};
export interface RouteSuccessResponse<TResponse extends z.ZodTypeAny> {
    readonly status?: number;
    readonly body: z.infer<TResponse>;
    readonly headers?: Record<string, string>;
}
export interface RouteErrorResponse {
    readonly status?: number;
    readonly errors: readonly ModuleError[];
    readonly headers?: Record<string, string>;
}
export type RouteHandlerResult<TResponse extends z.ZodTypeAny> = RouteSuccessResponse<TResponse> | RouteErrorResponse;
export type RouteHandler<TContext extends RequestContext, TParams extends z.ZodTypeAny | undefined, TQuery extends z.ZodTypeAny | undefined, TBody extends z.ZodTypeAny | undefined, TResponse extends z.ZodTypeAny> = (context: RouteHandlerContext<TContext, TParams, TQuery, TBody>) => Promise<RouteHandlerResult<TResponse>> | RouteHandlerResult<TResponse>;
export interface RouteSpec<TContext extends RequestContext = RequestContext, TParams extends z.ZodTypeAny | undefined = undefined, TQuery extends z.ZodTypeAny | undefined = undefined, TBody extends z.ZodTypeAny | undefined = undefined, TResponse extends z.ZodTypeAny = z.ZodTypeAny> {
    readonly definition: RouteDefinition;
    readonly schemas: RouteSchemas<TParams, TQuery, TBody, TResponse>;
    readonly handler: RouteHandler<TContext, TParams, TQuery, TBody, TResponse>;
}
export declare const viewDefinitionSchema: z.ZodObject<{
    name: z.ZodString;
    path: z.ZodString;
    summary: z.ZodOptional<z.ZodString>;
    description: z.ZodOptional<z.ZodString>;
    tags: z.ZodOptional<z.ZodArray<z.ZodString, "many">>;
    params: z.ZodOptional<z.ZodObject<{
        kind: z.ZodDefault<z.ZodEnum<["zod", "json-schema", "ts-rest"]>>;
        name: z.ZodString;
        source: z.ZodOptional<z.ZodString>;
    }, "strip", z.ZodTypeAny, {
        kind: "zod" | "json-schema" | "ts-rest";
        name: string;
        source?: string | undefined;
    }, {
        name: string;
        kind?: "zod" | "json-schema" | "ts-rest" | undefined;
        source?: string | undefined;
    }>>;
    data: z.ZodOptional<z.ZodObject<{
        kind: z.ZodDefault<z.ZodEnum<["zod", "json-schema", "ts-rest"]>>;
        name: z.ZodString;
        source: z.ZodOptional<z.ZodString>;
    }, "strip", z.ZodTypeAny, {
        kind: "zod" | "json-schema" | "ts-rest";
        name: string;
        source?: string | undefined;
    }, {
        name: string;
        kind?: "zod" | "json-schema" | "ts-rest" | undefined;
        source?: string | undefined;
    }>>;
}, "strip", z.ZodTypeAny, {
    name: string;
    path: string;
    params?: {
        kind: "zod" | "json-schema" | "ts-rest";
        name: string;
        source?: string | undefined;
    } | undefined;
    summary?: string | undefined;
    description?: string | undefined;
    tags?: string[] | undefined;
    data?: {
        kind: "zod" | "json-schema" | "ts-rest";
        name: string;
        source?: string | undefined;
    } | undefined;
}, {
    name: string;
    path: string;
    params?: {
        name: string;
        kind?: "zod" | "json-schema" | "ts-rest" | undefined;
        source?: string | undefined;
    } | undefined;
    summary?: string | undefined;
    description?: string | undefined;
    tags?: string[] | undefined;
    data?: {
        name: string;
        kind?: "zod" | "json-schema" | "ts-rest" | undefined;
        source?: string | undefined;
    } | undefined;
}>;
export type ViewDefinition = z.infer<typeof viewDefinitionSchema>;
export type ViewLoaderContext<TContext extends SSRContext, TParams extends z.ZodTypeAny | undefined> = TContext & {
    readonly params: InferOrNever<TParams>;
};
export type ViewLoader<TContext extends SSRContext, TParams extends z.ZodTypeAny | undefined, TData extends z.ZodTypeAny> = (context: ViewLoaderContext<TContext, TParams>) => Promise<z.infer<TData>> | z.infer<TData>;
export interface ViewSpec<TContext extends SSRContext = SSRContext, TParams extends z.ZodTypeAny | undefined = undefined, TData extends z.ZodTypeAny = z.ZodTypeAny> {
    readonly definition: ViewDefinition;
    readonly params?: TParams;
    readonly data: TData;
    readonly load: ViewLoader<TContext, TParams, TData>;
}
export declare const jobDefinitionSchema: z.ZodObject<{
    name: z.ZodString;
    schedule: z.ZodOptional<z.ZodString>;
    priority: z.ZodOptional<z.ZodUnion<[z.ZodNumber, z.ZodString]>>;
}, "strip", z.ZodTypeAny, {
    name: string;
    schedule?: string | undefined;
    priority?: string | number | undefined;
}, {
    name: string;
    schedule?: string | undefined;
    priority?: string | number | undefined;
}>;
export type JobDefinition = z.infer<typeof jobDefinitionSchema>;
export declare const eventDefinitionSchema: z.ZodObject<{
    name: z.ZodString;
    payload: z.ZodOptional<z.ZodObject<{
        kind: z.ZodDefault<z.ZodEnum<["zod", "json-schema", "ts-rest"]>>;
        name: z.ZodString;
        source: z.ZodOptional<z.ZodString>;
    }, "strip", z.ZodTypeAny, {
        kind: "zod" | "json-schema" | "ts-rest";
        name: string;
        source?: string | undefined;
    }, {
        name: string;
        kind?: "zod" | "json-schema" | "ts-rest" | undefined;
        source?: string | undefined;
    }>>;
    description: z.ZodOptional<z.ZodString>;
}, "strip", z.ZodTypeAny, {
    name: string;
    description?: string | undefined;
    payload?: {
        kind: "zod" | "json-schema" | "ts-rest";
        name: string;
        source?: string | undefined;
    } | undefined;
}, {
    name: string;
    description?: string | undefined;
    payload?: {
        name: string;
        kind?: "zod" | "json-schema" | "ts-rest" | undefined;
        source?: string | undefined;
    } | undefined;
}>;
export type EventDefinition = z.infer<typeof eventDefinitionSchema>;
export declare const serviceDefinitionSchema: z.ZodObject<{
    name: z.ZodString;
    description: z.ZodOptional<z.ZodString>;
}, "strip", z.ZodTypeAny, {
    name: string;
    description?: string | undefined;
}, {
    name: string;
    description?: string | undefined;
}>;
export type ServiceDefinition = z.infer<typeof serviceDefinitionSchema>;
export declare const moduleManifestSchema: z.ZodObject<{
    contractVersion: z.ZodString;
    name: z.ZodString;
    version: z.ZodString;
    kind: z.ZodEnum<["frontend", "backend"]>;
    capabilities: z.ZodOptional<z.ZodArray<z.ZodString, "many">>;
    routes: z.ZodOptional<z.ZodArray<z.ZodObject<{
        name: z.ZodString;
        method: z.ZodEnum<["GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"]>;
        path: z.ZodString;
        summary: z.ZodOptional<z.ZodString>;
        description: z.ZodOptional<z.ZodString>;
        tags: z.ZodOptional<z.ZodArray<z.ZodString, "many">>;
        input: z.ZodOptional<z.ZodObject<{
            params: z.ZodOptional<z.ZodOptional<z.ZodObject<{
                kind: z.ZodDefault<z.ZodEnum<["zod", "json-schema", "ts-rest"]>>;
                name: z.ZodString;
                source: z.ZodOptional<z.ZodString>;
            }, "strip", z.ZodTypeAny, {
                kind: "zod" | "json-schema" | "ts-rest";
                name: string;
                source?: string | undefined;
            }, {
                name: string;
                kind?: "zod" | "json-schema" | "ts-rest" | undefined;
                source?: string | undefined;
            }>>>;
            query: z.ZodOptional<z.ZodOptional<z.ZodObject<{
                kind: z.ZodDefault<z.ZodEnum<["zod", "json-schema", "ts-rest"]>>;
                name: z.ZodString;
                source: z.ZodOptional<z.ZodString>;
            }, "strip", z.ZodTypeAny, {
                kind: "zod" | "json-schema" | "ts-rest";
                name: string;
                source?: string | undefined;
            }, {
                name: string;
                kind?: "zod" | "json-schema" | "ts-rest" | undefined;
                source?: string | undefined;
            }>>>;
            body: z.ZodOptional<z.ZodOptional<z.ZodObject<{
                kind: z.ZodDefault<z.ZodEnum<["zod", "json-schema", "ts-rest"]>>;
                name: z.ZodString;
                source: z.ZodOptional<z.ZodString>;
            }, "strip", z.ZodTypeAny, {
                kind: "zod" | "json-schema" | "ts-rest";
                name: string;
                source?: string | undefined;
            }, {
                name: string;
                kind?: "zod" | "json-schema" | "ts-rest" | undefined;
                source?: string | undefined;
            }>>>;
            headers: z.ZodOptional<z.ZodOptional<z.ZodObject<{
                kind: z.ZodDefault<z.ZodEnum<["zod", "json-schema", "ts-rest"]>>;
                name: z.ZodString;
                source: z.ZodOptional<z.ZodString>;
            }, "strip", z.ZodTypeAny, {
                kind: "zod" | "json-schema" | "ts-rest";
                name: string;
                source?: string | undefined;
            }, {
                name: string;
                kind?: "zod" | "json-schema" | "ts-rest" | undefined;
                source?: string | undefined;
            }>>>;
        }, "strict", z.ZodTypeAny, {
            params?: {
                kind: "zod" | "json-schema" | "ts-rest";
                name: string;
                source?: string | undefined;
            } | undefined;
            query?: {
                kind: "zod" | "json-schema" | "ts-rest";
                name: string;
                source?: string | undefined;
            } | undefined;
            body?: {
                kind: "zod" | "json-schema" | "ts-rest";
                name: string;
                source?: string | undefined;
            } | undefined;
            headers?: {
                kind: "zod" | "json-schema" | "ts-rest";
                name: string;
                source?: string | undefined;
            } | undefined;
        }, {
            params?: {
                name: string;
                kind?: "zod" | "json-schema" | "ts-rest" | undefined;
                source?: string | undefined;
            } | undefined;
            query?: {
                name: string;
                kind?: "zod" | "json-schema" | "ts-rest" | undefined;
                source?: string | undefined;
            } | undefined;
            body?: {
                name: string;
                kind?: "zod" | "json-schema" | "ts-rest" | undefined;
                source?: string | undefined;
            } | undefined;
            headers?: {
                name: string;
                kind?: "zod" | "json-schema" | "ts-rest" | undefined;
                source?: string | undefined;
            } | undefined;
        }>>;
        output: z.ZodOptional<z.ZodObject<{
            body: z.ZodObject<{
                kind: z.ZodDefault<z.ZodEnum<["zod", "json-schema", "ts-rest"]>>;
                name: z.ZodString;
                source: z.ZodOptional<z.ZodString>;
            }, "strip", z.ZodTypeAny, {
                kind: "zod" | "json-schema" | "ts-rest";
                name: string;
                source?: string | undefined;
            }, {
                name: string;
                kind?: "zod" | "json-schema" | "ts-rest" | undefined;
                source?: string | undefined;
            }>;
            status: z.ZodOptional<z.ZodNumber>;
            headers: z.ZodOptional<z.ZodObject<{
                kind: z.ZodDefault<z.ZodEnum<["zod", "json-schema", "ts-rest"]>>;
                name: z.ZodString;
                source: z.ZodOptional<z.ZodString>;
            }, "strip", z.ZodTypeAny, {
                kind: "zod" | "json-schema" | "ts-rest";
                name: string;
                source?: string | undefined;
            }, {
                name: string;
                kind?: "zod" | "json-schema" | "ts-rest" | undefined;
                source?: string | undefined;
            }>>;
        }, "strip", z.ZodTypeAny, {
            body: {
                kind: "zod" | "json-schema" | "ts-rest";
                name: string;
                source?: string | undefined;
            };
            status?: number | undefined;
            headers?: {
                kind: "zod" | "json-schema" | "ts-rest";
                name: string;
                source?: string | undefined;
            } | undefined;
        }, {
            body: {
                name: string;
                kind?: "zod" | "json-schema" | "ts-rest" | undefined;
                source?: string | undefined;
            };
            status?: number | undefined;
            headers?: {
                name: string;
                kind?: "zod" | "json-schema" | "ts-rest" | undefined;
                source?: string | undefined;
            } | undefined;
        }>>;
        errors: z.ZodOptional<z.ZodArray<z.ZodObject<{
            code: z.ZodEnum<["validation", "auth", "not_found", "domain", "conflict", "internal"]>;
            message: z.ZodString;
            details: z.ZodOptional<z.ZodUnknown>;
            cause: z.ZodOptional<z.ZodUnknown>;
            correlationId: z.ZodOptional<z.ZodString>;
        }, "strip", z.ZodTypeAny, {
            code: "validation" | "auth" | "not_found" | "domain" | "conflict" | "internal";
            message: string;
            details?: unknown;
            cause?: unknown;
            correlationId?: string | undefined;
        }, {
            code: "validation" | "auth" | "not_found" | "domain" | "conflict" | "internal";
            message: string;
            details?: unknown;
            cause?: unknown;
            correlationId?: string | undefined;
        }>, "many">>;
    }, "strip", z.ZodTypeAny, {
        name: string;
        path: string;
        method: "GET" | "POST" | "DELETE" | "PUT" | "PATCH" | "HEAD" | "OPTIONS";
        summary?: string | undefined;
        description?: string | undefined;
        tags?: string[] | undefined;
        errors?: {
            code: "validation" | "auth" | "not_found" | "domain" | "conflict" | "internal";
            message: string;
            details?: unknown;
            cause?: unknown;
            correlationId?: string | undefined;
        }[] | undefined;
        input?: {
            params?: {
                kind: "zod" | "json-schema" | "ts-rest";
                name: string;
                source?: string | undefined;
            } | undefined;
            query?: {
                kind: "zod" | "json-schema" | "ts-rest";
                name: string;
                source?: string | undefined;
            } | undefined;
            body?: {
                kind: "zod" | "json-schema" | "ts-rest";
                name: string;
                source?: string | undefined;
            } | undefined;
            headers?: {
                kind: "zod" | "json-schema" | "ts-rest";
                name: string;
                source?: string | undefined;
            } | undefined;
        } | undefined;
        output?: {
            body: {
                kind: "zod" | "json-schema" | "ts-rest";
                name: string;
                source?: string | undefined;
            };
            status?: number | undefined;
            headers?: {
                kind: "zod" | "json-schema" | "ts-rest";
                name: string;
                source?: string | undefined;
            } | undefined;
        } | undefined;
    }, {
        name: string;
        path: string;
        method: "GET" | "POST" | "DELETE" | "PUT" | "PATCH" | "HEAD" | "OPTIONS";
        summary?: string | undefined;
        description?: string | undefined;
        tags?: string[] | undefined;
        errors?: {
            code: "validation" | "auth" | "not_found" | "domain" | "conflict" | "internal";
            message: string;
            details?: unknown;
            cause?: unknown;
            correlationId?: string | undefined;
        }[] | undefined;
        input?: {
            params?: {
                name: string;
                kind?: "zod" | "json-schema" | "ts-rest" | undefined;
                source?: string | undefined;
            } | undefined;
            query?: {
                name: string;
                kind?: "zod" | "json-schema" | "ts-rest" | undefined;
                source?: string | undefined;
            } | undefined;
            body?: {
                name: string;
                kind?: "zod" | "json-schema" | "ts-rest" | undefined;
                source?: string | undefined;
            } | undefined;
            headers?: {
                name: string;
                kind?: "zod" | "json-schema" | "ts-rest" | undefined;
                source?: string | undefined;
            } | undefined;
        } | undefined;
        output?: {
            body: {
                name: string;
                kind?: "zod" | "json-schema" | "ts-rest" | undefined;
                source?: string | undefined;
            };
            status?: number | undefined;
            headers?: {
                name: string;
                kind?: "zod" | "json-schema" | "ts-rest" | undefined;
                source?: string | undefined;
            } | undefined;
        } | undefined;
    }>, "many">>;
    views: z.ZodOptional<z.ZodArray<z.ZodObject<{
        name: z.ZodString;
        path: z.ZodString;
        summary: z.ZodOptional<z.ZodString>;
        description: z.ZodOptional<z.ZodString>;
        tags: z.ZodOptional<z.ZodArray<z.ZodString, "many">>;
        params: z.ZodOptional<z.ZodObject<{
            kind: z.ZodDefault<z.ZodEnum<["zod", "json-schema", "ts-rest"]>>;
            name: z.ZodString;
            source: z.ZodOptional<z.ZodString>;
        }, "strip", z.ZodTypeAny, {
            kind: "zod" | "json-schema" | "ts-rest";
            name: string;
            source?: string | undefined;
        }, {
            name: string;
            kind?: "zod" | "json-schema" | "ts-rest" | undefined;
            source?: string | undefined;
        }>>;
        data: z.ZodOptional<z.ZodObject<{
            kind: z.ZodDefault<z.ZodEnum<["zod", "json-schema", "ts-rest"]>>;
            name: z.ZodString;
            source: z.ZodOptional<z.ZodString>;
        }, "strip", z.ZodTypeAny, {
            kind: "zod" | "json-schema" | "ts-rest";
            name: string;
            source?: string | undefined;
        }, {
            name: string;
            kind?: "zod" | "json-schema" | "ts-rest" | undefined;
            source?: string | undefined;
        }>>;
    }, "strip", z.ZodTypeAny, {
        name: string;
        path: string;
        params?: {
            kind: "zod" | "json-schema" | "ts-rest";
            name: string;
            source?: string | undefined;
        } | undefined;
        summary?: string | undefined;
        description?: string | undefined;
        tags?: string[] | undefined;
        data?: {
            kind: "zod" | "json-schema" | "ts-rest";
            name: string;
            source?: string | undefined;
        } | undefined;
    }, {
        name: string;
        path: string;
        params?: {
            name: string;
            kind?: "zod" | "json-schema" | "ts-rest" | undefined;
            source?: string | undefined;
        } | undefined;
        summary?: string | undefined;
        description?: string | undefined;
        tags?: string[] | undefined;
        data?: {
            name: string;
            kind?: "zod" | "json-schema" | "ts-rest" | undefined;
            source?: string | undefined;
        } | undefined;
    }>, "many">>;
    jobs: z.ZodOptional<z.ZodArray<z.ZodObject<{
        name: z.ZodString;
        schedule: z.ZodOptional<z.ZodString>;
        priority: z.ZodOptional<z.ZodUnion<[z.ZodNumber, z.ZodString]>>;
    }, "strip", z.ZodTypeAny, {
        name: string;
        schedule?: string | undefined;
        priority?: string | number | undefined;
    }, {
        name: string;
        schedule?: string | undefined;
        priority?: string | number | undefined;
    }>, "many">>;
    events: z.ZodOptional<z.ZodArray<z.ZodObject<{
        name: z.ZodString;
        payload: z.ZodOptional<z.ZodObject<{
            kind: z.ZodDefault<z.ZodEnum<["zod", "json-schema", "ts-rest"]>>;
            name: z.ZodString;
            source: z.ZodOptional<z.ZodString>;
        }, "strip", z.ZodTypeAny, {
            kind: "zod" | "json-schema" | "ts-rest";
            name: string;
            source?: string | undefined;
        }, {
            name: string;
            kind?: "zod" | "json-schema" | "ts-rest" | undefined;
            source?: string | undefined;
        }>>;
        description: z.ZodOptional<z.ZodString>;
    }, "strip", z.ZodTypeAny, {
        name: string;
        description?: string | undefined;
        payload?: {
            kind: "zod" | "json-schema" | "ts-rest";
            name: string;
            source?: string | undefined;
        } | undefined;
    }, {
        name: string;
        description?: string | undefined;
        payload?: {
            name: string;
            kind?: "zod" | "json-schema" | "ts-rest" | undefined;
            source?: string | undefined;
        } | undefined;
    }>, "many">>;
    services: z.ZodOptional<z.ZodArray<z.ZodObject<{
        name: z.ZodString;
        description: z.ZodOptional<z.ZodString>;
    }, "strip", z.ZodTypeAny, {
        name: string;
        description?: string | undefined;
    }, {
        name: string;
        description?: string | undefined;
    }>, "many">>;
    init: z.ZodOptional<z.ZodString>;
    dispose: z.ZodOptional<z.ZodString>;
}, "strip", z.ZodTypeAny, {
    kind: "frontend" | "backend";
    name: string;
    contractVersion: string;
    version: string;
    capabilities?: string[] | undefined;
    routes?: {
        name: string;
        path: string;
        method: "GET" | "POST" | "DELETE" | "PUT" | "PATCH" | "HEAD" | "OPTIONS";
        summary?: string | undefined;
        description?: string | undefined;
        tags?: string[] | undefined;
        errors?: {
            code: "validation" | "auth" | "not_found" | "domain" | "conflict" | "internal";
            message: string;
            details?: unknown;
            cause?: unknown;
            correlationId?: string | undefined;
        }[] | undefined;
        input?: {
            params?: {
                kind: "zod" | "json-schema" | "ts-rest";
                name: string;
                source?: string | undefined;
            } | undefined;
            query?: {
                kind: "zod" | "json-schema" | "ts-rest";
                name: string;
                source?: string | undefined;
            } | undefined;
            body?: {
                kind: "zod" | "json-schema" | "ts-rest";
                name: string;
                source?: string | undefined;
            } | undefined;
            headers?: {
                kind: "zod" | "json-schema" | "ts-rest";
                name: string;
                source?: string | undefined;
            } | undefined;
        } | undefined;
        output?: {
            body: {
                kind: "zod" | "json-schema" | "ts-rest";
                name: string;
                source?: string | undefined;
            };
            status?: number | undefined;
            headers?: {
                kind: "zod" | "json-schema" | "ts-rest";
                name: string;
                source?: string | undefined;
            } | undefined;
        } | undefined;
    }[] | undefined;
    views?: {
        name: string;
        path: string;
        params?: {
            kind: "zod" | "json-schema" | "ts-rest";
            name: string;
            source?: string | undefined;
        } | undefined;
        summary?: string | undefined;
        description?: string | undefined;
        tags?: string[] | undefined;
        data?: {
            kind: "zod" | "json-schema" | "ts-rest";
            name: string;
            source?: string | undefined;
        } | undefined;
    }[] | undefined;
    jobs?: {
        name: string;
        schedule?: string | undefined;
        priority?: string | number | undefined;
    }[] | undefined;
    events?: {
        name: string;
        description?: string | undefined;
        payload?: {
            kind: "zod" | "json-schema" | "ts-rest";
            name: string;
            source?: string | undefined;
        } | undefined;
    }[] | undefined;
    services?: {
        name: string;
        description?: string | undefined;
    }[] | undefined;
    init?: string | undefined;
    dispose?: string | undefined;
}, {
    kind: "frontend" | "backend";
    name: string;
    contractVersion: string;
    version: string;
    capabilities?: string[] | undefined;
    routes?: {
        name: string;
        path: string;
        method: "GET" | "POST" | "DELETE" | "PUT" | "PATCH" | "HEAD" | "OPTIONS";
        summary?: string | undefined;
        description?: string | undefined;
        tags?: string[] | undefined;
        errors?: {
            code: "validation" | "auth" | "not_found" | "domain" | "conflict" | "internal";
            message: string;
            details?: unknown;
            cause?: unknown;
            correlationId?: string | undefined;
        }[] | undefined;
        input?: {
            params?: {
                name: string;
                kind?: "zod" | "json-schema" | "ts-rest" | undefined;
                source?: string | undefined;
            } | undefined;
            query?: {
                name: string;
                kind?: "zod" | "json-schema" | "ts-rest" | undefined;
                source?: string | undefined;
            } | undefined;
            body?: {
                name: string;
                kind?: "zod" | "json-schema" | "ts-rest" | undefined;
                source?: string | undefined;
            } | undefined;
            headers?: {
                name: string;
                kind?: "zod" | "json-schema" | "ts-rest" | undefined;
                source?: string | undefined;
            } | undefined;
        } | undefined;
        output?: {
            body: {
                name: string;
                kind?: "zod" | "json-schema" | "ts-rest" | undefined;
                source?: string | undefined;
            };
            status?: number | undefined;
            headers?: {
                name: string;
                kind?: "zod" | "json-schema" | "ts-rest" | undefined;
                source?: string | undefined;
            } | undefined;
        } | undefined;
    }[] | undefined;
    views?: {
        name: string;
        path: string;
        params?: {
            name: string;
            kind?: "zod" | "json-schema" | "ts-rest" | undefined;
            source?: string | undefined;
        } | undefined;
        summary?: string | undefined;
        description?: string | undefined;
        tags?: string[] | undefined;
        data?: {
            name: string;
            kind?: "zod" | "json-schema" | "ts-rest" | undefined;
            source?: string | undefined;
        } | undefined;
    }[] | undefined;
    jobs?: {
        name: string;
        schedule?: string | undefined;
        priority?: string | number | undefined;
    }[] | undefined;
    events?: {
        name: string;
        description?: string | undefined;
        payload?: {
            name: string;
            kind?: "zod" | "json-schema" | "ts-rest" | undefined;
            source?: string | undefined;
        } | undefined;
    }[] | undefined;
    services?: {
        name: string;
        description?: string | undefined;
    }[] | undefined;
    init?: string | undefined;
    dispose?: string | undefined;
}>;
export type ModuleManifest = z.infer<typeof moduleManifestSchema>;
export interface ModuleLifecycleContext {
    readonly env: EnvAccessor;
    readonly logger: Logger;
}
export type ModuleLifecycleHook = (context: ModuleLifecycleContext) => Promise<void> | void;
export interface ModuleDefinition<TRequestContext extends RequestContext = RequestContext, TSSRContext extends SSRContext = SSRContext, TRoutes extends readonly RouteSpec<TRequestContext, any, any, any, any>[] = readonly RouteSpec<TRequestContext, any, any, any, any>[], TViews extends readonly ViewSpec<TSSRContext, any, any>[] = readonly ViewSpec<TSSRContext, any, any>[]> {
    readonly manifest: ModuleManifest;
    readonly routes?: TRoutes;
    readonly views?: TViews;
    readonly init?: ModuleLifecycleHook;
    readonly dispose?: ModuleLifecycleHook;
}
export interface BackendProvider<TDefinition extends ModuleDefinition = ModuleDefinition> extends ModuleProvider {
    readonly module: TDefinition;
}
export interface AuthProviderCapability {
    readonly auth: AuthProvider | undefined;
}
export interface DatabaseProviderCapability {
    readonly database: DatabaseProvider | undefined;
}
export interface CacheProviderCapability {
    readonly cache: CacheProvider | undefined;
}
export interface QueueProviderCapability {
    readonly queue: QueueProvider | undefined;
}
export declare function defineRoute<TContext extends RequestContext, TParams extends z.ZodTypeAny | undefined = undefined, TQuery extends z.ZodTypeAny | undefined = undefined, TBody extends z.ZodTypeAny | undefined = undefined, TResponse extends z.ZodTypeAny = z.ZodTypeAny>(spec: RouteSpec<TContext, TParams, TQuery, TBody, TResponse>): RouteSpec<TContext, TParams, TQuery, TBody, TResponse>;
export declare function defineView<TContext extends SSRContext, TParams extends z.ZodTypeAny | undefined = undefined, TData extends z.ZodTypeAny = z.ZodTypeAny>(spec: ViewSpec<TContext, TParams, TData>): ViewSpec<TContext, TParams, TData>;
export declare function createModule<TRequestContext extends RequestContext, TSSRContext extends SSRContext, TRoutes extends readonly RouteSpec<TRequestContext, any, any, any, any>[] = readonly RouteSpec<TRequestContext, any, any, any, any>[], TViews extends readonly ViewSpec<TSSRContext, any, any>[] = readonly ViewSpec<TSSRContext, any, any>[]>(definition: ModuleDefinition<TRequestContext, TSSRContext, TRoutes, TViews>): ModuleDefinition<TRequestContext, TSSRContext, TRoutes, TViews>;
export { fromTsRestRoute, fromTsRestRouter } from './adapters/ts-rest.js';
export type { FromTsRestRouteOptions, FromTsRestRouterOptions, RouterRouteConfig } from './adapters/ts-rest.js';
