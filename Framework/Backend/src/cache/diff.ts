import path from 'node:path';
import { readFile, writeFile, mkdir } from 'node:fs/promises';

import type { ModuleManifest, ModuleDiagnostic } from '@webstir-io/module-contract';

import type { BackendBuildMode } from '../workspace.js';

export async function persistAndDiffOutputs(
    workspaceRoot: string,
    _buildRoot: string,
    outputs: Record<string, number> | undefined,
    env: Record<string, string | undefined>,
    diagnostics: ModuleDiagnostic[],
    mode: BackendBuildMode
): Promise<void> {
    if (!outputs) return;
    try {
        const diagMax = resolveDiagMax(env);
        const webstirDir = path.join(workspaceRoot, '.webstir');
        const cachePath = path.join(webstirDir, 'backend-outputs.json');
        await mkdir(webstirDir, { recursive: true });

        let previous: Record<string, number> = {};
        try {
            const raw = await readFile(cachePath, 'utf8');
            previous = JSON.parse(raw) as Record<string, number>;
        } catch {
            // first run or unreadable cache
        }

        const changed: string[] = [];
        for (const [rel, bytes] of Object.entries(outputs)) {
            if (previous[rel] !== bytes) {
                changed.push(rel);
            }
        }
        const removed = Object.keys(previous).filter((rel) => outputs[rel] === undefined);

        if (changed.length + removed.length > 0) {
            const list = changed.slice(0, diagMax).join(', ');
            const omitted = changed.length > diagMax ? ` (+${changed.length - diagMax} more)` : '';
            const removedInfo = removed.length > 0 ? `, removed=${removed.length}` : '';
            diagnostics.push({
                severity: 'info',
                message: `[webstir-backend] ${mode}:changed ${changed.length} file(s): ${list}${omitted}${removedInfo}`
            });
        }

        await writeFile(cachePath, JSON.stringify(outputs, null, 2), 'utf8');
    } catch {
        // ignore cache errors
    }
}

export async function persistAndDiffManifest(
    workspaceRoot: string,
    manifest: ModuleManifest,
    env: Record<string, string | undefined>,
    diagnostics: ModuleDiagnostic[]
): Promise<void> {
    try {
        const diagMax = resolveDiagMax(env);
        const webstirDir = path.join(workspaceRoot, '.webstir');
        const cachePath = path.join(webstirDir, 'backend-manifest-digest.json');
        await mkdir(webstirDir, { recursive: true });

        const routeKeys = Array.isArray(manifest.routes)
            ? (manifest.routes as any[]).map((r) => `${(r.method ?? '').toUpperCase()} ${r.path ?? ''}`)
            : [];
        const viewPaths = Array.isArray(manifest.views)
            ? (manifest.views as any[]).map((v) => `${v.path ?? ''}`)
            : [];
        const caps = Array.isArray(manifest.capabilities) ? manifest.capabilities : [];

        type Digest = { routes: string[]; views: string[]; capabilities: string[] };
        let previous: Digest | undefined;
        try {
            const raw = await readFile(cachePath, 'utf8');
            previous = JSON.parse(raw) as Digest;
        } catch {
            // first run; no diff
        }

        if (previous) {
            const prevRoutes = new Set(previous.routes);
            const prevViews = new Set(previous.views);
            const nextRoutes = new Set(routeKeys);
            const nextViews = new Set(viewPaths);

            const addedRoutes: string[] = [];
            const removedRoutes: string[] = [];
            const addedViews: string[] = [];
            const removedViews: string[] = [];

            for (const r of nextRoutes) if (!prevRoutes.has(r)) addedRoutes.push(r);
            for (const r of prevRoutes) if (!nextRoutes.has(r)) removedRoutes.push(r);
            for (const v of nextViews) if (!prevViews.has(v)) addedViews.push(v);
            for (const v of prevViews) if (!nextViews.has(v)) removedViews.push(v);

            if (addedRoutes.length + removedRoutes.length + addedViews.length + removedViews.length > 0) {
                const list = (items: string[]) => items.slice(0, diagMax).join(', ');
                const routeDelta = `routes +${addedRoutes.length}/-${removedRoutes.length}`;
                const viewDelta = `views +${addedViews.length}/-${removedViews.length}`;
                let msg = `[webstir-backend] manifest changed: ${routeDelta}; ${viewDelta}`;
                const details: string[] = [];
                if (addedRoutes.length > 0) details.push(`added routes: ${list(addedRoutes)}`);
                if (removedRoutes.length > 0) details.push(`removed routes: ${list(removedRoutes)}`);
                if (addedViews.length > 0) details.push(`added views: ${list(addedViews)}`);
                if (removedViews.length > 0) details.push(`removed views: ${list(removedViews)}`);
                if (details.length > 0) {
                    msg += ` — ${details.join(' | ')}`;
                }
                diagnostics.push({ severity: 'info', message: msg });
            }
        }

        const digest: Digest = { routes: routeKeys, views: viewPaths, capabilities: caps };
        await writeFile(cachePath, JSON.stringify(digest, null, 2), 'utf8');
    } catch {
        // ignore cache errors
    }
}

function resolveDiagMax(env: Record<string, string | undefined>, fallback = 50): number {
    const raw = env?.WEBSTIR_BACKEND_DIAG_MAX;
    const n = typeof raw === 'string' ? parseInt(raw, 10) : NaN;
    return Number.isFinite(n) && n > 0 ? n : fallback;
}
