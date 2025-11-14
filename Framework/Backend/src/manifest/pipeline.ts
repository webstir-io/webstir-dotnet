import path from 'node:path';
import { existsSync } from 'node:fs';
import { readFile } from 'node:fs/promises';
import { pathToFileURL } from 'node:url';

import { moduleManifestSchema, CONTRACT_VERSION } from '@webstir-io/module-contract';
import type { ModuleDefinition, ModuleDiagnostic, ModuleManifest } from '@webstir-io/module-contract';

interface WorkspacePackageJson {
    readonly name?: string;
    readonly version?: string;
    readonly webstir?: {
        readonly module?: WorkspaceModuleConfig;
    };
}

type WorkspaceModuleConfig = Partial<ModuleManifest> & {
    readonly contractVersion?: string;
    readonly capabilities?: ModuleManifest['capabilities'];
};

export interface LoadManifestOptions {
    readonly workspaceRoot: string;
    readonly buildRoot: string;
    readonly entryPoints: readonly string[];
    readonly diagnostics: ModuleDiagnostic[];
}

export async function loadBackendModuleManifest(options: LoadManifestOptions): Promise<ModuleManifest> {
    const { workspaceRoot, buildRoot, entryPoints, diagnostics } = options;
    const pkgPath = path.join(workspaceRoot, 'package.json');
    let workspacePackage: WorkspacePackageJson | undefined;

    try {
        const raw = await readFile(pkgPath, 'utf8');
        workspacePackage = JSON.parse(raw) as WorkspacePackageJson;
    } catch (error) {
        diagnostics.push({
            severity: 'warn',
            message: `[webstir-backend] unable to read ${pkgPath}: ${(error as Error).message}. Using defaults.`
        });
    }

    const moduleConfig = workspacePackage?.webstir?.module ?? {};

    let manifestCandidate: ModuleManifest = {
        contractVersion: typeof moduleConfig.contractVersion === 'string' ? moduleConfig.contractVersion : CONTRACT_VERSION,
        name: typeof moduleConfig.name === 'string' ? moduleConfig.name : deriveModuleName(workspacePackage, workspaceRoot),
        version: typeof moduleConfig.version === 'string' ? moduleConfig.version : deriveModuleVersion(workspacePackage),
        kind: 'backend',
        capabilities: Array.isArray(moduleConfig.capabilities) ? moduleConfig.capabilities : [],
        routes: moduleConfig.routes ?? [],
        views: moduleConfig.views ?? [],
        jobs: moduleConfig.jobs ?? [],
        events: moduleConfig.events ?? [],
        services: moduleConfig.services ?? [],
        init: moduleConfig.init,
        dispose: moduleConfig.dispose
    };

    const definition = await loadModuleDefinition(buildRoot, diagnostics);
    if (definition) {
        const definitionManifest = definition.manifest ?? ({} as ModuleManifest);
        const routesFromDefinition = definition.routes?.map((route) => route.definition);
        const viewsFromDefinition = definition.views?.map((view) => view.definition);

        const mergedCapabilities = Array.from(
            new Set([...(manifestCandidate.capabilities ?? []), ...(definitionManifest.capabilities ?? [])])
        );

        manifestCandidate = {
            ...manifestCandidate,
            ...definitionManifest,
            capabilities: mergedCapabilities,
            routes: routesFromDefinition ?? definitionManifest.routes ?? manifestCandidate.routes ?? [],
            views: viewsFromDefinition ?? definitionManifest.views ?? manifestCandidate.views ?? [],
            jobs: definitionManifest.jobs ?? manifestCandidate.jobs ?? [],
            events: definitionManifest.events ?? manifestCandidate.events ?? [],
            services: definitionManifest.services ?? manifestCandidate.services ?? [],
            init: definitionManifest.init ?? manifestCandidate.init,
            dispose: definitionManifest.dispose ?? manifestCandidate.dispose
        };
    }

    const validation = moduleManifestSchema.safeParse(manifestCandidate);
    if (!validation.success) {
        const problems = validation.error.issues
            .map((issue) => `${issue.path.join('.') || '(root)'}: ${issue.message}`)
            .join('; ');
        diagnostics.push({
            severity: 'error',
            message: `[webstir-backend] module manifest validation failed (${problems}). Falling back to defaults.`
        });
        return {
            contractVersion: CONTRACT_VERSION,
            name: deriveModuleName(workspacePackage, workspaceRoot),
            version: deriveModuleVersion(workspacePackage),
            kind: 'backend',
            capabilities: [],
            routes: [],
            views: [],
            jobs: [],
            events: [],
            services: []
        };
    }

    const manifest = validation.data;

    try {
        const normalizePath = (p: unknown) => {
            let s = typeof p === 'string' ? p : '';
            if (!s.startsWith('/')) s = '/' + s;
            s = s.replace(/\/+/, '/');
            if (s.length > 1 && s.endsWith('/')) s = s.slice(0, -1);
            return s;
        };
        const seen = new Map<string, number>();
        for (const r of manifest.routes ?? []) {
            const method = typeof (r as any).method === 'string' ? (r as any).method.toUpperCase() : '';
            const pathKey = normalizePath((r as any).path);
            const key = `${method} ${pathKey}`;
            seen.set(key, (seen.get(key) ?? 0) + 1);
        }
        const dups = Array.from(seen.entries()).filter(([, count]) => count > 1);
        if (dups.length > 0) {
            const list = dups.map(([k, c]) => `${k} (${c}x)`).join(', ');
            diagnostics.push({ severity: 'warn', message: `[webstir-backend] duplicate route definitions: ${list}` });
        }
    } catch {
        // best-effort only
    }

    if (manifest.routes?.length && entryPoints.length === 0) {
        diagnostics.push({
            severity: 'warn',
            message: '[webstir-backend] module manifest defines routes but no entry points were built. Ensure backend compilation produced handlers.'
        });
    }

    try {
        const jobs = Array.isArray(manifest.jobs) ? manifest.jobs : [];
        const events = Array.isArray(manifest.events) ? manifest.events : [];
        const services = Array.isArray(manifest.services) ? manifest.services : [];
        if (jobs.length + events.length + services.length > 0) {
            diagnostics.push({
                severity: 'info',
                message: `[webstir-backend] manifest jobs=${jobs.length} events=${events.length} services=${services.length}`
            });
        }

        const noSchedule = jobs.filter((j: any) => j && typeof j.name === 'string' && (j.schedule === undefined || j.schedule === null));
        if (noSchedule.length > 0) {
            const MAX_LIST = 10;
            const names = noSchedule.map((j: any) => j.name).slice(0, MAX_LIST).join(', ');
            const omitted = noSchedule.length > MAX_LIST ? ` (+${noSchedule.length - MAX_LIST} more)` : '';
            diagnostics.push({
                severity: 'warn',
                message: `[webstir-backend] jobs without schedules: ${names}${omitted}`
            });
        }
    } catch {
        // best-effort only
    }

    try {
        const routes = Array.isArray(manifest.routes) ? manifest.routes.length : 0;
        const views = Array.isArray(manifest.views) ? manifest.views.length : 0;
        const caps = Array.isArray(manifest.capabilities) && manifest.capabilities.length > 0 ? ` [${manifest.capabilities.join(', ')}]` : '';
        diagnostics.push({ severity: 'info', message: `[webstir-backend] manifest routes=${routes} views=${views}${caps}` });
    } catch {
        // ignore
    }

    return manifest;
}

async function loadModuleDefinition(
    buildRoot: string,
    diagnostics: ModuleDiagnostic[]
): Promise<ModuleDefinition | undefined> {
    const candidates = [
        path.join(buildRoot, 'module.js'),
        path.join(buildRoot, 'module.mjs'),
        path.join(buildRoot, 'module/index.js'),
        path.join(buildRoot, 'module/index.mjs')
    ];

    for (const fullPath of candidates) {
        if (!existsSync(fullPath)) {
            continue;
        }

        try {
            const moduleUrl = `${pathToFileURL(fullPath).href}?t=${Date.now()}`;
            const imported = (await import(moduleUrl)) as Record<string, unknown>;
            const definitionCandidate = extractModuleDefinition(imported);
            if (isModuleDefinition(definitionCandidate)) {
                return definitionCandidate;
            }
            diagnostics.push({
                severity: 'warn',
                message: `[webstir-backend] module definition at ${fullPath} does not export a createModule() definition.`
            });
        } catch (error) {
            diagnostics.push({
                severity: 'warn',
                message: `[webstir-backend] failed to load module definition from ${fullPath}: ${(error as Error).message}`
            });
        }
    }

    return undefined;
}

function extractModuleDefinition(exports: Record<string, unknown>): unknown {
    const keys = ['module', 'moduleDefinition', 'default', 'backendModule'];
    for (const key of keys) {
        if (key in exports) {
            const value = exports[key as keyof typeof exports];
            if (value !== null && value !== undefined) {
                return value;
            }
        }
    }
    return undefined;
}

function isModuleDefinition(value: unknown): value is ModuleDefinition {
    return typeof value === 'object' && value !== null && 'manifest' in (value as Record<string, unknown>);
}

function deriveModuleName(pkg: WorkspacePackageJson | undefined, workspaceRoot: string): string {
    if (typeof pkg?.webstir?.module?.name === 'string' && pkg.webstir.module.name.length > 0) {
        return pkg.webstir.module.name;
    }
    if (typeof pkg?.name === 'string' && pkg.name.length > 0) {
        return pkg.name;
    }
    return `backend-module-${path.basename(workspaceRoot)}`;
}

function deriveModuleVersion(pkg: WorkspacePackageJson | undefined): string {
    if (typeof pkg?.webstir?.module?.version === 'string' && pkg.webstir.module.version.length > 0) {
        return pkg.webstir.module.version;
    }
    if (typeof pkg?.version === 'string' && pkg.version.length > 0) {
        return pkg.version;
    }
    return '0.0.0';
}

export async function summarizeBuiltManifest(
    buildRoot: string
): Promise<{ routes: number; views: number; capabilities?: readonly string[] } | undefined> {
    const definition = await loadModuleDefinition(buildRoot, []);
    if (!definition || !definition.manifest) {
        return undefined;
    }
    const manifest = definition.manifest;
    return {
        routes: Array.isArray(manifest.routes) ? manifest.routes.length : 0,
        views: Array.isArray(manifest.views) ? manifest.views.length : 0,
        capabilities: manifest.capabilities
    };
}
