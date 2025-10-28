import path from 'node:path';
import { existsSync } from 'node:fs';
import { readFile } from 'node:fs/promises';
import { spawn } from 'node:child_process';
import { performance } from 'node:perf_hooks';
import { fileURLToPath, pathToFileURL } from 'node:url';

import { build as esbuild } from 'esbuild';

import { glob } from 'glob';
import { moduleManifestSchema } from '@webstir-io/module-contract';
import type {
    ModuleArtifact,
    ModuleAsset,
    ModuleBuildOptions,
    ModuleBuildResult,
    ModuleDefinition,
    ModuleDiagnostic,
    ModuleManifest,
    ModuleProvider,
    ResolvedModuleWorkspace
} from '@webstir-io/module-contract';

import packageJson from '../package.json' with { type: 'json' };

interface PackageJson {
    readonly name: string;
    readonly version: string;
    readonly engines?: {
        readonly node?: string;
    };
}

const pkg = packageJson as PackageJson;

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

function resolveWorkspacePaths(workspaceRoot: string): ResolvedModuleWorkspace {
    return {
        sourceRoot: path.join(workspaceRoot, 'src', 'backend'),
        buildRoot: path.join(workspaceRoot, 'build', 'backend'),
        testsRoot: path.join(workspaceRoot, 'src', 'backend', 'tests')
    };
}

export const backendProvider: ModuleProvider = {
    metadata: {
        id: pkg.name ?? '@webstir-io/webstir-backend',
        kind: 'backend',
        version: pkg.version ?? '0.0.0',
        compatibility: {
            minCliVersion: '0.1.0',
            nodeRange: pkg.engines?.node ?? '>=20.18.1'
        }
    },
    resolveWorkspace(options) {
        return resolveWorkspacePaths(options.workspaceRoot);
    },
    async build(options) {
        const paths = resolveWorkspacePaths(options.workspaceRoot);
        const tsconfigPath = path.join(paths.sourceRoot, 'tsconfig.json');

        const diagnostics: ModuleDiagnostic[] = [];

        const incremental = options.incremental === true;
        const mode = normalizeMode(options.env?.WEBSTIR_MODULE_MODE);
        console.info(`[webstir-backend] ${mode}:start`);

        // 1) Type-check with TSC (no emit) if a tsconfig exists.
        console.info(`[webstir-backend] ${mode}:tsc start`);
        if (shouldTypeCheck(mode, options.env)) {
            await runTypeCheck(tsconfigPath, options.env, diagnostics);
        } else {
            diagnostics.push({ severity: 'info', message: '[webstir-backend] type-check skipped by WEBSTIR_BACKEND_TYPECHECK' });
        }
        console.info(`[webstir-backend] ${mode}:tsc done`);

        // 2) Transpile/bundle with esbuild based on mode.
        const entryPoints = await discoverEntryPoints(paths.sourceRoot);
        if (entryPoints.length === 0) {
            diagnostics.push({ severity: 'warn', message: `No backend entry points found under ${paths.sourceRoot} (expected index.* or functions/*/index.* or jobs/*/index.*).` });
        }
        console.info(`[webstir-backend] ${mode}:esbuild start`);
        await runEsbuild({
            sourceRoot: paths.sourceRoot,
            buildRoot: paths.buildRoot,
            tsconfigPath,
            mode,
            env: options.env,
            incremental,
            diagnostics,
            entryPoints
        });
        console.info(`[webstir-backend] ${mode}:esbuild done`);

        const moduleSource = await discoverModuleDefinitionSource(paths.sourceRoot);

        if (moduleSource) {
            await buildModuleDefinition({
                sourceFile: moduleSource,
                sourceRoot: paths.sourceRoot,
                buildRoot: paths.buildRoot,
                tsconfigPath,
                mode,
                env: options.env,
                diagnostics
            });
        }

        const artifacts = await collectArtifacts(paths.buildRoot);
        const moduleManifest = await loadWorkspaceModuleManifest(options.workspaceRoot, paths.buildRoot, entryPoints, diagnostics);
        const manifest = createManifest(paths.buildRoot, artifacts, diagnostics, moduleManifest);

        console.info(`[webstir-backend] ${mode}:complete (entries=${manifest.entryPoints.length})`);
        return {
            artifacts,
            manifest
        };
    },
    async getScaffoldAssets() {
        return await getScaffoldAssets();
    }
};

function normalizeMode(rawMode: unknown): 'build' | 'publish' | 'test' {
    if (typeof rawMode !== 'string') {
        return 'build';
    }
    const normalized = rawMode.toLowerCase();
    if (normalized === 'publish' || normalized === 'test') {
        return normalized;
    }
    return 'build';
}

async function collectArtifacts(buildRoot: string): Promise<ModuleArtifact[]> {
    const matches = await glob('**/*.js', {
        cwd: buildRoot,
        nodir: true,
        dot: false
    });

    return matches.map<ModuleArtifact>((relativePath) => ({
        path: path.join(buildRoot, relativePath),
        type: 'bundle'
    }));
}

async function loadWorkspaceModuleManifest(
    workspaceRoot: string,
    buildRoot: string,
    entryPoints: readonly string[],
    diagnostics: ModuleDiagnostic[]
): Promise<ModuleManifest> {
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
        contractVersion: typeof moduleConfig.contractVersion === 'string' ? moduleConfig.contractVersion : '1.0.0',
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
            contractVersion: '1.0.0',
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

    if (manifest.routes?.length && entryPoints.length === 0) {
        diagnostics.push({
            severity: 'warn',
            message: '[webstir-backend] module manifest defines routes but no entry points were built. Ensure backend compilation produced handlers.'
        });
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

function createManifest(
    buildRoot: string,
    artifacts: readonly ModuleArtifact[],
    diagnostics: ModuleDiagnostic[],
    moduleManifest: ModuleManifest
) {
    const entryPoints: string[] = [];

    for (const artifact of artifacts) {
        const relative = path.relative(buildRoot, artifact.path);
        if (relative.endsWith('index.js')) {
            entryPoints.push(relative);
        }
    }

    if (entryPoints.length === 0) {
        const defaultEntry = path.join(buildRoot, 'index.js');
        if (existsSync(defaultEntry)) {
            entryPoints.push(path.relative(buildRoot, defaultEntry));
        } else {
            diagnostics.push({
                severity: 'warn',
                message: 'No backend entry point found (expected index.js).'
            });
        }
    }

    return {
        entryPoints,
        staticAssets: [],
        diagnostics,
        module: moduleManifest
    };
}

async function runTypeCheck(tsconfigPath: string, env: Record<string, string | undefined>, diagnostics: ModuleDiagnostic[]): Promise<void> {
    if (!existsSync(tsconfigPath)) {
        diagnostics.push({
            severity: 'warn',
            message: `TypeScript config not found at ${tsconfigPath}; skipping type-check.`
        });
        return;
    }

    await new Promise<void>((resolve, reject) => {
        const child = spawn('tsc', ['-p', tsconfigPath, '--noEmit'], {
            stdio: 'pipe',
            env: {
                ...process.env,
                ...env
            }
        });

        let stdout = '';
        let stderr = '';

        child.stdout?.on('data', (chunk) => {
            stdout += chunk.toString();
        });

        child.stderr?.on('data', (chunk) => {
            stderr += chunk.toString();
        });

        child.on('error', (err: any) => {
            const code = (err && typeof err === 'object') ? (err.code as string | undefined) : undefined;
            if (code === 'ENOENT') {
                diagnostics.push({ severity: 'warn', message: 'TypeScript compiler (tsc) not found in PATH; skipping type-check.' });
                resolve();
                return;
            }
            reject(err);
        });
        child.on('close', (code) => {
            if (code === 0) {
                resolve();
            } else {
                diagnostics.push({
                    severity: 'error',
                    message: `Type checking failed (exit code ${code}).`,
                    file: tsconfigPath
                });
                if (stderr.trim()) {
                    diagnostics.push({ severity: 'error', message: stderr.trim() });
                }
                if (stdout.trim()) {
                    diagnostics.push({ severity: 'info', message: stdout.trim() });
                }
                reject(new Error('Type checking failed.'));
            }
        });
    });
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

function shouldTypeCheck(mode: 'build' | 'publish' | 'test', env: Record<string, string | undefined>): boolean {
    if (mode === 'publish') {
        return true;
    }
    const flag = env?.WEBSTIR_BACKEND_TYPECHECK;
    if (typeof flag === 'string' && flag.toLowerCase() === 'skip') {
        return false;
    }
    return true;
}

interface BuildOptions {
    readonly sourceRoot: string;
    readonly buildRoot: string;
    readonly tsconfigPath: string;
    readonly mode: 'build' | 'publish' | 'test';
    readonly env: Record<string, string | undefined>;
    readonly incremental: boolean;
    readonly diagnostics: ModuleDiagnostic[];
    readonly entryPoints: readonly string[];
}

async function runEsbuild(options: BuildOptions): Promise<void> {
    const { sourceRoot, buildRoot, tsconfigPath, mode, env, diagnostics, entryPoints } = options;
    if (!entryPoints || entryPoints.length === 0) {
        return;
    }

    const isProduction = mode === 'publish';
    const nodeEnv = env?.NODE_ENV ?? (isProduction ? 'production' : 'development');

    const define: Record<string, string> = {
        'process.env.NODE_ENV': JSON.stringify(nodeEnv)
    };

    const start = performance.now();
    try {
        if (isProduction) {
            const result = await esbuild({
                entryPoints: entryPoints as string[],
                bundle: true,
                packages: 'external',
                platform: 'node',
                target: 'node20',
                format: 'esm',
                minify: true,
                sourcemap: false,
                legalComments: 'none',
                outdir: buildRoot,
                outbase: sourceRoot,
                entryNames: '[dir]/[name]',
                tsconfig: existsSync(tsconfigPath) ? tsconfigPath : undefined,
                define,
                logLevel: 'silent',
                metafile: true
            });

            const warnCount = result.warnings?.length ?? 0;
            for (const w of result.warnings ?? []) {
                diagnostics.push({ severity: 'warn', message: formatEsbuildMessage(w) });
            }
            const end = performance.now();
            diagnostics.push({ severity: 'info', message: `[webstir-backend] publish:esbuild completed in ${(end - start).toFixed(1)}ms with ${warnCount} warning(s).` });
        } else {
            const result = await esbuild({
                entryPoints: entryPoints as string[],
                bundle: false,
                platform: 'node',
                target: 'node20',
                format: 'esm',
                sourcemap: true,
                outdir: buildRoot,
                outbase: sourceRoot,
                tsconfig: existsSync(tsconfigPath) ? tsconfigPath : undefined,
                define,
                logLevel: 'silent'
            });

            const warnCount = result.warnings?.length ?? 0;
            for (const w of result.warnings ?? []) {
                diagnostics.push({ severity: 'warn', message: formatEsbuildMessage(w) });
            }
            const end = performance.now();
            diagnostics.push({ severity: 'info', message: `[webstir-backend] ${mode}:esbuild completed in ${(end - start).toFixed(1)}ms with ${warnCount} warning(s).` });
        }
    } catch (error) {
        const end = performance.now();
        if (isEsbuildFailure(error)) {
            for (const e of error.errors ?? []) {
                diagnostics.push({ severity: 'error', message: formatEsbuildMessage(e) });
            }
            for (const w of error.warnings ?? []) {
                diagnostics.push({ severity: 'warn', message: formatEsbuildMessage(w) });
            }
        } else if (error instanceof Error) {
            diagnostics.push({ severity: 'error', message: error.message });
        } else {
            diagnostics.push({ severity: 'error', message: String(error) });
        }
        diagnostics.push({ severity: 'info', message: `[webstir-backend] ${mode}:esbuild failed after ${(end - start).toFixed(1)}ms.` });
        throw new Error('esbuild failed.');
    }
}

async function discoverEntryPoints(sourceRoot: string): Promise<string[]> {
    const patterns = [
        'index.{ts,tsx,js,mjs}',
        'functions/*/index.{ts,tsx,js,mjs}',
        'jobs/*/index.{ts,tsx,js,mjs}'
    ];
    const entries = new Set<string>();
    for (const pattern of patterns) {
        const matches = await glob(pattern, { cwd: sourceRoot, nodir: true, dot: false });
        for (const rel of matches) {
            entries.add(path.join(sourceRoot, rel));
        }
    }
    return Array.from(entries);
}

async function discoverModuleDefinitionSource(sourceRoot: string): Promise<string | undefined> {
    const patterns = [
        'module.{ts,tsx,js,mjs}',
        'module/index.{ts,tsx,js,mjs}'
    ];

    for (const pattern of patterns) {
        const matches = await glob(pattern, {
            cwd: sourceRoot,
            absolute: true,
            nodir: true,
            dot: false
        });

        if (matches.length > 0) {
            return matches[0];
        }
    }

    return undefined;
}

interface ModuleDefinitionBuildOptions {
    readonly sourceFile: string;
    readonly sourceRoot: string;
    readonly buildRoot: string;
    readonly tsconfigPath: string;
    readonly mode: 'build' | 'publish' | 'test';
    readonly env?: Record<string, string | undefined>;
    readonly diagnostics: ModuleDiagnostic[];
}

async function buildModuleDefinition(options: ModuleDefinitionBuildOptions): Promise<void> {
    const { sourceFile, sourceRoot, buildRoot, tsconfigPath, mode, env, diagnostics } = options;

    const isProduction = mode === 'publish';
    const nodeEnv = env?.NODE_ENV ?? (isProduction ? 'production' : 'development');
    const define: Record<string, string> = {
        'process.env.NODE_ENV': JSON.stringify(nodeEnv)
    };

    try {
        await esbuild({
            entryPoints: [sourceFile],
            bundle: false,
            platform: 'node',
            target: 'node20',
            format: 'esm',
            sourcemap: !isProduction,
            outdir: buildRoot,
            outbase: sourceRoot,
            entryNames: '[dir]/[name]',
            tsconfig: existsSync(tsconfigPath) ? tsconfigPath : undefined,
            define,
            logLevel: 'silent'
        });
    } catch (error) {
        if (isEsbuildFailure(error)) {
            for (const e of error.errors ?? []) {
                diagnostics.push({ severity: 'error', message: formatEsbuildMessage(e) });
            }
            for (const w of error.warnings ?? []) {
                diagnostics.push({ severity: 'warn', message: formatEsbuildMessage(w) });
            }
        } else if (error instanceof Error) {
            diagnostics.push({ severity: 'error', message: error.message });
        } else {
            diagnostics.push({ severity: 'error', message: String(error) });
        }
    }
}

function isEsbuildFailure(error: unknown): error is { errors?: readonly any[]; warnings?: readonly any[] } {
    return typeof error === 'object' && error !== null && ('errors' in (error as any) || 'warnings' in (error as any));
}

function formatEsbuildMessage(msg: any): string {
    const text = typeof msg?.text === 'string' ? msg.text : String(msg);
    const loc = msg?.location;
    if (loc && typeof loc.file === 'string') {
        const position = typeof loc.line === 'number' ? `${loc.line}:${loc.column ?? 1}` : '1:1';
        return `${loc.file}:${position} ${text}`;
    }
    return text;
}

async function getScaffoldAssets(): Promise<readonly ModuleAsset[]> {
    const here = path.dirname(fileURLToPath(import.meta.url));
    const packageRoot = path.resolve(here, '..');
    const templatesRoot = path.join(packageRoot, 'templates', 'backend');

    return [
        {
            sourcePath: path.join(templatesRoot, 'tsconfig.json'),
            targetPath: path.join('src', 'backend', 'tsconfig.json')
        },
        {
            sourcePath: path.join(templatesRoot, 'index.ts'),
            targetPath: path.join('src', 'backend', 'index.ts')
        }
    ];
}
