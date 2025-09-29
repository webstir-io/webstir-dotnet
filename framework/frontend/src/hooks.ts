import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import type { FrontendConfig } from './types.js';
import type { PipelineMode } from './pipeline.js';

const CONFIG_CANDIDATES = ['webstir.config.mjs', 'webstir.config.js', 'webstir.config.cjs'];

export interface HookContext {
    readonly config: FrontendConfig;
    readonly mode: PipelineMode;
    readonly workspaceRoot: string;
    readonly builderName?: string;
    readonly changedFile?: string;
}

export type HookHandler = (context: HookContext) => unknown | Promise<unknown>;

export interface ResolvedHooks {
    readonly pipelineBefore: HookHandler[];
    readonly pipelineAfter: HookHandler[];
    readonly builderBefore: Map<string, HookHandler[]>;
    readonly builderAfter: Map<string, HookHandler[]>;
}

interface RawHooks {
    readonly pipeline?: RawPipelineHooks;
    readonly builders?: Record<string, RawBuilderHooks | HookHandler | HookHandler[]>;
}

interface RawPipelineHooks {
    readonly beforeAll?: HookHandler | HookHandler[];
    readonly afterAll?: HookHandler | HookHandler[];
}

interface RawBuilderHooks {
    readonly before?: HookHandler | HookHandler[];
    readonly after?: HookHandler | HookHandler[];
}

const EMPTY_HOOKS: ResolvedHooks = {
    pipelineBefore: [],
    pipelineAfter: [],
    builderBefore: new Map(),
    builderAfter: new Map()
};

export async function loadHooks(workspaceRoot: string, cacheBust: boolean): Promise<ResolvedHooks> {
    const configPath = findConfigPath(workspaceRoot);
    if (!configPath) {
        return EMPTY_HOOKS;
    }

    let moduleConfig: unknown;
    try {
        const fileUrl = pathToFileURL(configPath).href;
        const url = cacheBust ? `${fileUrl}?update=${Date.now()}` : fileUrl;
        moduleConfig = await import(url);
    } catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        throw new Error(`Failed to load hooks from ${configPath}: ${message}`);
    }

    const exported = (moduleConfig as Record<string, unknown>)?.default ?? moduleConfig;
    if (!exported || typeof exported !== 'object') {
        return EMPTY_HOOKS;
    }

    const rawHooks = (exported as Record<string, unknown>).hooks ?? exported;
    if (!rawHooks || typeof rawHooks !== 'object') {
        return EMPTY_HOOKS;
    }

    return normalizeHooks(rawHooks as RawHooks, configPath);
}

export function createHookContext(
    config: FrontendConfig,
    mode: PipelineMode,
    changedFile: string | undefined,
    builderName?: string
): HookContext {
    return {
        config,
        mode,
        workspaceRoot: config.paths.workspace,
        builderName,
        changedFile
    };
}

export async function executeHooks(label: string, handlers: HookHandler[], context: HookContext): Promise<void> {
    for (const handler of handlers) {
        try {
            await handler(context);
        } catch (error) {
            throw wrapHookError(label, error);
        }
    }
}

function wrapHookError(label: string, error: unknown): Error {
    if (error instanceof Error) {
        error.message = `[hook:${label}] ${error.message}`;
        return error;
    }

    return new Error(`[hook:${label}] ${String(error)}`);
}

function normalizeHooks(raw: RawHooks, configPath: string): ResolvedHooks {
    const pipeline = raw.pipeline ?? {};
    const pipelineBefore = normalizeHandlerSet(pipeline.beforeAll, `${configPath} pipeline.beforeAll`);
    const pipelineAfter = normalizeHandlerSet(pipeline.afterAll, `${configPath} pipeline.afterAll`);

    const builderBefore = new Map<string, HookHandler[]>();
    const builderAfter = new Map<string, HookHandler[]>();

    const builders = raw.builders ?? {};
    for (const [name, value] of Object.entries(builders)) {
        if (typeof value === 'function' || Array.isArray(value)) {
            builderBefore.set(name, normalizeHandlerSet(value, `${configPath} builders.${name}`));
            continue;
        }

        if (!value || typeof value !== 'object') {
            continue;
        }

        const before = normalizeHandlerSet(value.before, `${configPath} builders.${name}.before`);
        const after = normalizeHandlerSet(value.after, `${configPath} builders.${name}.after`);
        if (before.length > 0) {
            builderBefore.set(name, before);
        }
        if (after.length > 0) {
            builderAfter.set(name, after);
        }
    }

    return {
        pipelineBefore,
        pipelineAfter,
        builderBefore,
        builderAfter
    };
}

function normalizeHandlerSet(value: HookHandler | HookHandler[] | undefined, label: string): HookHandler[] {
    if (value === undefined) {
        return [];
    }

    const handlers = Array.isArray(value) ? value : [value];
    const normalized: HookHandler[] = [];

    for (const handler of handlers) {
        if (typeof handler !== 'function') {
            throw new Error(`Invalid hook handler in ${label}; expected function`);
        }
        normalized.push(handler);
    }

    return normalized;
}

function findConfigPath(workspaceRoot: string): string | undefined {
    for (const candidate of CONFIG_CANDIDATES) {
        const fullPath = path.join(workspaceRoot, candidate);
        if (fs.existsSync(fullPath)) {
            return fullPath;
        }
    }

    return undefined;
}
