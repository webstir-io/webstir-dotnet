import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
const CONFIG_CANDIDATES = ['webstir.config.mjs', 'webstir.config.js', 'webstir.config.cjs'];
const EMPTY_HOOKS = {
    pipelineBefore: [],
    pipelineAfter: [],
    builderBefore: new Map(),
    builderAfter: new Map()
};
export async function loadHooks(workspaceRoot, cacheBust) {
    const configPath = findConfigPath(workspaceRoot);
    if (!configPath) {
        return EMPTY_HOOKS;
    }
    let moduleConfig;
    try {
        const fileUrl = pathToFileURL(configPath).href;
        const url = cacheBust ? `${fileUrl}?update=${Date.now()}` : fileUrl;
        moduleConfig = await import(url);
    }
    catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        throw new Error(`Failed to load hooks from ${configPath}: ${message}`);
    }
    const exported = moduleConfig?.default ?? moduleConfig;
    if (!exported || typeof exported !== 'object') {
        return EMPTY_HOOKS;
    }
    const rawHooks = exported.hooks ?? exported;
    if (!rawHooks || typeof rawHooks !== 'object') {
        return EMPTY_HOOKS;
    }
    return normalizeHooks(rawHooks, configPath);
}
export function createHookContext(config, mode, changedFile, builderName) {
    return {
        config,
        mode,
        workspaceRoot: config.paths.workspace,
        builderName,
        changedFile
    };
}
export async function executeHooks(label, handlers, context) {
    for (const handler of handlers) {
        try {
            await handler(context);
        }
        catch (error) {
            throw wrapHookError(label, error);
        }
    }
}
function wrapHookError(label, error) {
    if (error instanceof Error) {
        error.message = `[hook:${label}] ${error.message}`;
        return error;
    }
    return new Error(`[hook:${label}] ${String(error)}`);
}
function normalizeHooks(raw, configPath) {
    const pipeline = raw.pipeline ?? {};
    const pipelineBefore = normalizeHandlerSet(pipeline.beforeAll, `${configPath} pipeline.beforeAll`);
    const pipelineAfter = normalizeHandlerSet(pipeline.afterAll, `${configPath} pipeline.afterAll`);
    const builderBefore = new Map();
    const builderAfter = new Map();
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
function normalizeHandlerSet(value, label) {
    if (value === undefined) {
        return [];
    }
    const handlers = Array.isArray(value) ? value : [value];
    const normalized = [];
    for (const handler of handlers) {
        if (typeof handler !== 'function') {
            throw new Error(`Invalid hook handler in ${label}; expected function`);
        }
        normalized.push(handler);
    }
    return normalized;
}
function findConfigPath(workspaceRoot) {
    for (const candidate of CONFIG_CANDIDATES) {
        const fullPath = path.join(workspaceRoot, candidate);
        if (fs.existsSync(fullPath)) {
            return fullPath;
        }
    }
    return undefined;
}
