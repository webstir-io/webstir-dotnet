#!/usr/bin/env node
import { Console } from 'node:console';
import fs from 'node:fs/promises';
import path from 'node:path';
import process from 'node:process';

const EVENT_PREFIX = 'WEBSTIR_TEST ';
const MODULE_EVENT_PREFIX = 'WEBSTIR_MODULE_EVENT ';
const SRC_FOLDER = 'src';
const BUILD_FOLDER = 'build';
const TEST_FOLDER = 'tests';
const BACKEND_FOLDER = 'backend';
const TEST_SUFFIXES = ['.test.ts', '.test.js'];
const EXCLUDED_DIRECTORIES = new Set(['node_modules', 'build', 'dist', '.git']);
const RUNTIME_FILTER = normalizeRuntimeFilter(process.env.WEBSTIR_TEST_RUNTIME);

overrideConsole();

await main();

async function main() {
  const args = parseArgs(process.argv.slice(2));

  try {
    validateArgs(args);

    const workspaceRoot = path.resolve(args.workspace);
    process.chdir(workspaceRoot);

    const registry = await loadProviderRegistry(args.provider);
    const runId = createRunId();

    try {
      const manifest = await discoverTestManifest(workspaceRoot);
      const filteredManifest = applyRuntimeFilter(manifest, RUNTIME_FILTER);
      if (RUNTIME_FILTER) {
        emitEvent({
          type: 'log',
          runId,
          level: 'info',
          message: runtimeFilterMessage(RUNTIME_FILTER, manifest.modules.length, filteredManifest.modules.length),
        });
      }
      emitEvent({
        type: 'start',
        runId,
        manifest: filteredManifest,
      });

      if (filteredManifest.modules.length === 0) {
        emitEvent({
          type: 'log',
          runId,
          level: 'info',
          message: 'No tests found under src/**/tests/.',
        });
        emitEvent(makeOverallSummary(runId, createEmptySummary()));
        return;
      }

      const summary = await executeRun(runId, filteredManifest, registry);
      emitEvent(makeOverallSummary(runId, summary));

      if ((summary?.failed ?? 0) > 0) {
        process.exitCode = 1;
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      const stack = error instanceof Error ? error.stack ?? undefined : undefined;
      emitEvent({
        type: 'error',
        runId,
        message,
        stack,
      });
      process.exitCode = 1;
    }
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    process.stderr.write(`${message}\n`);
    process.exitCode = 1;
  }
}

function parseArgs(argv) {
  const result = {};

  for (let index = 0; index < argv.length; index += 1) {
    const value = argv[index];
    switch (value) {
      case '--provider':
        result.provider = argv[++index];
        break;
      case '--workspace':
        result.workspace = argv[++index];
        break;
      default:
        break;
    }
  }

  return result;
}

function validateArgs(args) {
  if (!args.provider) {
    throw new Error('Missing required --provider <module> argument.');
  }

  if (!args.workspace) {
    throw new Error('Missing required --workspace <path> argument.');
  }

  if (!path.isAbsolute(args.workspace)) {
    throw new Error(`Workspace must be an absolute path (received: ${args.workspace}).`);
  }
}

async function loadProviderRegistry(providerId) {
  const module = await import(providerId);
  const registry = await resolveRegistry(module, providerId);

  if (!registry || typeof registry.get !== 'function') {
    throw new Error(`Provider '${providerId}' did not return a registry with a get(runtime) method.`);
  }

  return registry;
}

async function resolveRegistry(module, providerId) {
  const candidates = [];

  if (module && typeof module === 'object') {
    if (typeof module.createProviderRegistry === 'function') {
      candidates.push(module.createProviderRegistry);
    }
    if (typeof module.createDefaultProviderRegistry === 'function') {
      candidates.push(module.createDefaultProviderRegistry);
    }
    if (module.registry && typeof module.registry.get === 'function') {
      return module.registry;
    }
  }

  if (typeof module === 'function') {
    candidates.push(module);
  }

  const defaultExport = module?.default;
  if (defaultExport) {
    if (typeof defaultExport === 'function') {
      candidates.push(defaultExport);
    } else if (typeof defaultExport.createProviderRegistry === 'function') {
      candidates.push(defaultExport.createProviderRegistry.bind(defaultExport));
    } else if (typeof defaultExport.createDefaultProviderRegistry === 'function') {
      candidates.push(defaultExport.createDefaultProviderRegistry.bind(defaultExport));
    } else if (typeof defaultExport.get === 'function') {
      return defaultExport;
    }
  }

  for (const factory of candidates) {
    try {
      const value = await factory();
      if (value && typeof value.get === 'function') {
        return value;
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      throw new Error(`Failed to initialize provider '${providerId}': ${message}`);
    }
  }

  throw new Error(`Unable to resolve provider registry from '${providerId}'.`);
}

async function discoverTestManifest(workspaceRoot) {
  const absoluteRoot = path.resolve(workspaceRoot);
  const srcRoot = path.join(absoluteRoot, SRC_FOLDER);

  let srcExists = false;
  try {
    const stats = await fs.stat(srcRoot);
    srcExists = stats.isDirectory();
  } catch {
    srcExists = false;
  }

  if (!srcExists) {
    return {
      workspaceRoot: absoluteRoot,
      generatedAt: new Date().toISOString(),
      modules: [],
    };
  }

  const modules = [];
  await walkDirectory(srcRoot, async (filePath) => {
    const relativeToSrc = path.relative(srcRoot, filePath);
    if (relativeToSrc.startsWith('..')) {
      return;
    }

    if (!isUnderTestsFolder(relativeToSrc)) {
      return;
    }

    if (!isTestFile(filePath)) {
      return;
    }

    const runtime = inferRuntime(relativeToSrc);
    const compiledPath = computeCompiledPath(absoluteRoot, relativeToSrc);

    modules.push({
      id: normalizeModuleId(relativeToSrc),
      runtime,
      sourcePath: path.resolve(srcRoot, relativeToSrc),
      compiledPath,
    });
  });

  modules.sort((a, b) => a.id.localeCompare(b.id));

  return {
    workspaceRoot: absoluteRoot,
    generatedAt: new Date().toISOString(),
    modules,
  };
}

async function walkDirectory(root, onFile) {
  let entries;
  try {
    entries = await fs.readdir(root, { withFileTypes: true });
  } catch {
    return;
  }

  for (const entry of entries) {
    const entryPath = path.join(root, entry.name);

    if (entry.isDirectory()) {
      if (EXCLUDED_DIRECTORIES.has(entry.name) || entry.name.startsWith('.')) {
        continue;
      }

      await walkDirectory(entryPath, onFile);
      continue;
    }

    if (entry.isFile()) {
      await onFile(entryPath);
    }
  }
}

function isTestFile(filePath) {
  return TEST_SUFFIXES.some((suffix) => filePath.endsWith(suffix));
}

function isUnderTestsFolder(relativePath) {
  return splitPath(relativePath).includes(TEST_FOLDER);
}

function splitPath(relativePath) {
  return relativePath.split(/[\\/]+/).filter((segment) => segment.length > 0);
}

function inferRuntime(relativePath) {
  const segments = splitPath(relativePath);
  if (segments.length === 0) {
    return 'frontend';
  }

  return segments[0] === BACKEND_FOLDER ? 'backend' : 'frontend';
}

function computeCompiledPath(workspaceRoot, relativeToSrc) {
  const compiledRelative = replaceExtension(relativeToSrc, '.js');
  return path.join(workspaceRoot, BUILD_FOLDER, compiledRelative);
}

function replaceExtension(relativePath, newExtension) {
  const ext = path.extname(relativePath);
  if (!ext) {
    return `${relativePath}${newExtension}`;
  }

  return `${relativePath.slice(0, -ext.length)}${newExtension}`;
}

function normalizeModuleId(relativePath) {
  return splitPath(relativePath).join('/');
}

async function executeRun(runId, manifest, registry) {
  let accumulator = createEmptySummary();
  const byRuntime = groupModulesByRuntime(manifest.modules);

  for (const [runtime, modules] of byRuntime.entries()) {
    const provider = registry.get(runtime);
    if (!provider) {
      emitEvent({
        type: 'log',
        runId,
        level: 'warn',
        message: `Skipping ${modules.length} test${modules.length === 1 ? '' : 's'} for unsupported runtime '${runtime}'.`,
      });
      continue;
    }

    const summary = await runWithProvider(runId, runtime, modules, provider);
    accumulator = mergeSummaries(accumulator, summary);
  }

  return accumulator;
}

function groupModulesByRuntime(modules) {
  const result = new Map();
  for (const module of modules ?? []) {
    const list = result.get(module.runtime);
    if (list) {
      list.push(module);
    } else {
      result.set(module.runtime, [module]);
    }
  }

  return result;
}

async function runWithProvider(runId, runtime, modules, provider) {
  const files = [];
  const moduleByPath = new Map();

  for (const module of modules) {
    if (!module?.compiledPath) {
      emitEvent({
        type: 'log',
        runId,
        level: 'warn',
        message: `Test ${module?.id ?? '<unknown>'} has no compiled output; skipping.`,
      });
      continue;
    }

    const absolute = path.resolve(module.compiledPath);
    moduleByPath.set(absolute, module);
    files.push(absolute);
  }

  if (files.length === 0) {
    const emptySummary = createEmptySummary();
    emitEvent(makeRuntimeSummary(runId, runtime, emptySummary));
    return emptySummary;
  }

  const summary = await provider.runTests(files);
  for (const result of summary?.results ?? []) {
    const absolute = path.resolve(result.file);
    const module = moduleByPath.get(absolute);
    emitEvent({
      type: 'result',
      runId,
      runtime,
      moduleId: module?.id ?? absolute,
      result,
    });
  }

  emitEvent(makeRuntimeSummary(runId, runtime, summary));
  return summary ?? createEmptySummary();
}

function createEmptySummary() {
  return {
    passed: 0,
    failed: 0,
    total: 0,
    durationMs: 0,
    results: [],
  };
}

function mergeSummaries(left, right) {
  return {
    passed: (left?.passed ?? 0) + (right?.passed ?? 0),
    failed: (left?.failed ?? 0) + (right?.failed ?? 0),
    total: (left?.total ?? 0) + (right?.total ?? 0),
    durationMs: (left?.durationMs ?? 0) + (right?.durationMs ?? 0),
    results: [...(left?.results ?? []), ...(right?.results ?? [])],
  };
}

function makeRuntimeSummary(runId, runtime, summary) {
  return {
    type: 'summary',
    runId,
    runtime,
    summary,
  };
}

function makeOverallSummary(runId, summary) {
  return {
    type: 'summary',
    runId,
    runtime: 'all',
    summary,
  };
}

function emitEvent(event) {
  process.stdout.write(`${EVENT_PREFIX}${JSON.stringify(event)}\n`);
}

function createRunId() {
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`;
}

function overrideConsole() {
  const stdoutConsole = new Console(process.stdout, process.stdout);
  const stderrConsole = new Console(process.stderr, process.stderr);

  const forward = (level, targetConsole) => (...values) => {
    const message = values.map((value) => stringify(value)).join(' ');
    const payload = {
      type: level,
      message,
    };
    process.stderr.write(`${MODULE_EVENT_PREFIX}${JSON.stringify(payload)}\n`);
    targetConsole[level](...values);
  };

  console.log = forward('log', stdoutConsole);
  console.info = forward('info', stdoutConsole);
  console.warn = forward('warn', stderrConsole);
  console.error = forward('error', stderrConsole);
}

function stringify(value) {
  if (typeof value === 'string') {
    return value;
  }

  try {
    return JSON.stringify(value);
  } catch {
    return String(value);
  }
}

function normalizeRuntimeFilter(value) {
  if (!value) {
    return null;
  }

  const normalized = value.trim().toLowerCase();
  if (!normalized || normalized === 'all') {
    return null;
  }

  if (normalized === 'frontend' || normalized === 'backend') {
    return normalized;
  }

  return null;
}

function applyRuntimeFilter(manifest, runtime) {
  if (!runtime) {
    return manifest;
  }

  const modules = manifest.modules.filter((module) => module?.runtime === runtime);
  return {
    ...manifest,
    modules,
  };
}

function runtimeFilterMessage(runtime, beforeCount, afterCount) {
  const skipped = Math.max(beforeCount - afterCount, 0);
  return `Runtime filter '${runtime}' matched ${afterCount} test${afterCount === 1 ? '' : 's'} (${skipped} skipped).`;
}
