import path from 'node:path';
import { existsSync } from 'node:fs';
import { spawn, type ChildProcess } from 'node:child_process';
import { performance } from 'node:perf_hooks';
import { context as createEsbuildContext, type BuildContext, type BuildResult, type Plugin } from 'esbuild';

import type { ModuleDiagnostic } from '@webstir-io/module-contract';

import { collectOutputSizes, formatEsbuildMessage, shouldTypeCheck } from './build/pipeline.js';
import { discoverEntryPoints } from './build/entries.js';
import { loadBackendModuleManifest } from './manifest/pipeline.js';
import { createCacheReporter } from './cache/reporters.js';
import { normalizeMode, resolveWorkspacePaths } from './workspace.js';

export interface WatchHandle {
  stop(): Promise<void>;
}

export interface StartWatchOptions {
  readonly workspaceRoot: string;
  readonly env?: Record<string, string | undefined>;
}

export async function startBackendWatch(options: StartWatchOptions): Promise<WatchHandle> {
  const { workspaceRoot } = options;
  const env = options.env ?? {};
  const paths = resolveWorkspacePaths(workspaceRoot);
  const tsconfigPath = path.join(paths.sourceRoot, 'tsconfig.json');
  const mode = normalizeMode(env.WEBSTIR_MODULE_MODE);

  const entryPoints = await discoverEntryPoints(paths.sourceRoot);
  if (entryPoints.length === 0) {
    console.warn(`[webstir-backend] watch: no entry found under ${paths.sourceRoot} (index.ts/js)`);
    throw new Error('No backend entry point found.');
  }

  const nodeEnv = env.NODE_ENV ?? (mode === 'publish' ? 'production' : 'development');
  const diagMax = (() => {
    const raw = env.WEBSTIR_BACKEND_DIAG_MAX;
    const n = typeof raw === 'string' ? parseInt(raw, 10) : NaN;
    return Number.isFinite(n) && n > 0 ? n : 20;
  })();

  console.info(`[webstir-backend] watch:start (${mode})`);

  // Start type-checker in watch mode (no emit) unless explicitly skipped for DX.
  const shouldRunTypecheck = shouldTypeCheck(mode, env);
  let tscProc: ChildProcess | undefined;
  if (shouldRunTypecheck) {
    const tscArgs = ['-p', tsconfigPath, '--noEmit', '--watch'];
    tscProc = spawn('tsc', tscArgs, {
      stdio: ['ignore', 'pipe', 'pipe'],
      env: { ...process.env, ...env, NODE_ENV: nodeEnv },
      cwd: workspaceRoot,
    });

    tscProc.stdout?.on('data', (chunk) => {
      const text = chunk.toString();
      for (const line of text.split(/\r?\n/)) {
        if (line) console.info(`[webstir-backend][tsc] ${line}`);
      }
    });
    tscProc.stderr?.on('data', (chunk) => {
      const text = chunk.toString();
      for (const line of text.split(/\r?\n/)) {
        if (line) console.warn(`[webstir-backend][tsc] ${line}`);
      }
    });
  } else {
    console.info('[webstir-backend] watch: type-check skipped by WEBSTIR_BACKEND_TYPECHECK');
  }

  const timingPlugin: Plugin = {
    name: 'webstir-watch-logger',
    setup(build) {
      let start = 0;
      build.onStart(() => {
        start = performance.now();
      });
      build.onEnd(async (result: BuildResult) => {
        const end = performance.now();
        const warnCount = result.warnings?.length ?? 0;
        // errors is not in the typed result, but present at runtime
        const errorList = (result as any).errors ?? [];
        const errorCount = Array.isArray(errorList) ? errorList.length : 0;
        // Print detailed diagnostics with file:line when available (capped for readability)
        if (errorCount > 0) {
          for (const msg of errorList.slice(0, diagMax)) {
            const text = formatEsbuildMessage(msg);
            console.error(`[webstir-backend][esbuild] ${text}`);
          }
          if (errorCount > diagMax) {
            console.error(`[webstir-backend][esbuild] ... ${errorCount - diagMax} more error(s) omitted`);
          }
        }
        if (warnCount > 0) {
          for (const msg of result.warnings.slice(0, diagMax)) {
            const text = formatEsbuildMessage(msg as any);
            console.warn(`[webstir-backend][esbuild] ${text}`);
          }
          if (warnCount > diagMax) {
            console.warn(`[webstir-backend][esbuild] ... ${warnCount - diagMax} more warning(s) omitted`);
          }
        }
        console.info(`[webstir-backend] watch:esbuild ${errorCount} error(s), ${warnCount} warning(s) in ${(end - start).toFixed(1)}ms`);

        if (errorCount === 0) {
          const diagBuffer: ModuleDiagnostic[] = [];
          const cacheReporter = createCacheReporter({
            workspaceRoot,
            buildRoot: paths.buildRoot,
            env,
            diagnostics: diagBuffer
          });
          try {
            const metafile: any = (result as any).metafile;
            if (metafile && metafile.outputs) {
              const outputs = collectOutputSizes(metafile, paths.buildRoot);
              await cacheReporter.diffOutputs(outputs, mode);
            }
            const manifest = await loadBackendModuleManifest({
              workspaceRoot,
              buildRoot: paths.buildRoot,
              entryPoints,
              diagnostics: diagBuffer
            });
            await cacheReporter.diffManifest(manifest);
          } catch {
            // cache or manifest diff failure should not break watch
          } finally {
            for (const diag of diagBuffer) {
              const logger =
                diag.severity === 'error' ? console.error : diag.severity === 'warn' ? console.warn : console.info;
              logger(diag.message);
            }
          }
        }
      });
    },
  };

  const ctx: BuildContext = await createEsbuildContext({
    entryPoints,
    bundle: false,
    platform: 'node',
    target: 'node20',
    format: 'esm',
    sourcemap: true,
    outdir: paths.buildRoot,
    outbase: paths.sourceRoot,
    metafile: true,
    tsconfig: existsSync(tsconfigPath) ? tsconfigPath : undefined,
    define: { 'process.env.NODE_ENV': JSON.stringify(nodeEnv) },
    logLevel: 'silent',
    plugins: [timingPlugin],
  });

  await ctx.watch();

  console.info('[webstir-backend] watch:ready');

  return {
    async stop() {
      try {
        await ctx.dispose();
      } catch {
        // ignore
      }
      try {
        tscProc?.kill('SIGINT');
      } catch {
        // ignore
      }
      console.info('[webstir-backend] watch:stopped');
    },
  };
}
