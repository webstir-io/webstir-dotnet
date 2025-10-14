import fs from 'node:fs';
import Module from 'node:module';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import vm from 'node:vm';

import { assert } from './assert.js';
import type { AssertApi } from './assert.js';
import type { RegisteredTest, RunnerSummary, TestCallback, TestRunResult } from './types.js';

const registry = new Map<string, RegisteredTest[]>();
const moduleExports = Object.freeze({
  get test() {
    return test;
  },
  get assert() {
    return assert;
  },
});

type RequireFn = NodeJS.Require;
type RunnerGlobal = typeof globalThis & {
  __currentFile?: string;
  test?: (name: string, callback?: TestCallback) => void;
  assert?: AssertApi;
};

const runnerGlobal = globalThis as RunnerGlobal;

export function ensureRegistryFor(file: string): RegisteredTest[] {
  let list = registry.get(file);
  if (!list) {
    list = [];
    registry.set(file, list);
  }

  return list;
}

export function test(name: string, callback?: TestCallback): void {
  const currentFile = runnerGlobal.__currentFile;
  if (!currentFile) {
    throw new Error('No current file set');
  }

  const safeCallback: TestCallback = callback ?? (async () => undefined);
  ensureRegistryFor(currentFile).push({
    name: String(name),
    fn: safeCallback,
  });
}

runnerGlobal.test = test;
runnerGlobal.assert = assert;

export function createRuntimeRequire(file: string): RequireFn {
  const baseRequire = Module.createRequire(file);

  const runtimeRequire = ((specifier: string) => {
    if (specifier === '@webstir-io/webstir-test') {
      return moduleExports;
    }

    return baseRequire(specifier);
  }) as RequireFn;

  const resolve = ((specifier: string, options?: Parameters<typeof baseRequire.resolve>[1]) => {
    if (specifier === '@webstir-io/webstir-test') {
      return specifier;
    }

    return baseRequire.resolve(specifier, options);
  }) as NodeJS.RequireResolve;

  resolve.paths = baseRequire.resolve.paths;

  runtimeRequire.resolve = resolve;

  runtimeRequire.cache = baseRequire.cache;
  runtimeRequire.main = baseRequire.main;
  runtimeRequire.extensions = baseRequire.extensions;

  return runtimeRequire;
}

const esmFallback = Symbol('esm-fallback');

export async function evaluateModule(file: string): Promise<string | null> {
  const code = fs.readFileSync(file, 'utf8');
  const commonJsResult = evaluateCommonJsModule(file, code);

  if (commonJsResult === null) {
    return null;
  }

  if (commonJsResult === esmFallback) {
    return await evaluateEsmModule(file);
  }

  return commonJsResult;
}

export async function run(files: readonly string[]): Promise<RunnerSummary> {
  const allResults: TestRunResult[] = [];
  const start = Date.now();

  for (const file of files) {
    if (!fs.existsSync(file)) {
      allResults.push({
        name: '[missing compiled file]',
        file,
        passed: false,
        message: 'Compiled file not found',
        durationMs: 0,
      });
      continue;
    }

    const evalError = await evaluateModule(file);
    if (evalError) {
      allResults.push({
        name: '[module evaluation]',
        file,
        passed: false,
        message: evalError,
        durationMs: 0,
      });
      continue;
    }

    const tests = registry.get(file) ?? [];
    for (const entry of tests) {
      const outcome = await runSingleTest(entry);
      allResults.push({
        name: entry.name,
        file,
        passed: outcome.passed,
        message: outcome.message,
        durationMs: outcome.durationMs,
      });
    }
  }

  let passed = 0;
  let failed = 0;
  for (const result of allResults) {
    if (result.passed) {
      passed += 1;
    } else {
      failed += 1;
    }
  }

  return {
    passed,
    failed,
    total: allResults.length,
    durationMs: Date.now() - start,
    results: allResults,
  };
}

function evaluateCommonJsModule(file: string, code: string): string | typeof esmFallback | null {
  const runtimeRequire = createRuntimeRequire(file);
  const context = vm.createContext({
    test,
    assert,
    globalThis,
    console,
    setTimeout,
    clearTimeout,
    require: runtimeRequire,
    __dirname: path.dirname(file),
    __filename: file,
  });

  runnerGlobal.__currentFile = file;
  registry.set(file, []);

  try {
    const script = new vm.Script(code, { filename: file });
    script.runInContext(context, { displayErrors: true });
    return null;
  } catch (error) {
    if (isEsModuleSyntaxError(error)) {
      return esmFallback;
    }

    return formatError(error);
  } finally {
    delete runnerGlobal.__currentFile;
  }
}

async function evaluateEsmModule(file: string): Promise<string | null> {
  const moduleUrl = pathToFileURL(file);
  moduleUrl.searchParams.set('ts', Date.now().toString());

  try {
    runnerGlobal.__currentFile = file;
    registry.set(file, []);
    await import(moduleUrl.href);
    return null;
  } catch (error) {
    return formatError(error);
  } finally {
    delete runnerGlobal.__currentFile;
  }
}

function isEsModuleSyntaxError(error: unknown): boolean {
  if (!(error instanceof SyntaxError) || typeof error.message !== 'string') {
    return false;
  }

  return error.message.includes('Cannot use import statement outside a module')
    || error.message.includes('Unexpected token')
    || error.message.includes('export');
}

async function runSingleTest(testCase: RegisteredTest): Promise<{ passed: boolean; message: string | null; durationMs: number; }> {
  const start = Date.now();
  try {
    const result = testCase.fn();
    if (isPromiseLike(result)) {
      await result;
    }

    return {
      passed: true,
      message: null,
      durationMs: Date.now() - start,
    };
  } catch (error) {
    return {
      passed: false,
      message: formatError(error),
      durationMs: Date.now() - start,
    };
  }
}

function isPromiseLike(value: unknown): value is Promise<unknown> {
  return typeof value === 'object' && value !== null && 'then' in value && typeof (value as { then?: unknown }).then === 'function';
}

function formatError(error: unknown): string {
  if (error instanceof Error) {
    return error.stack ?? error.message;
  }

  return String(error);
}
