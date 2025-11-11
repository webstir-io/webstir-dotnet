import { createBackendTestHarness, setBackendTestContext } from '../backend/harness.js';
import { run as runVmRuntime } from '../runtime.js';
import type { TestProvider } from '../providers.js';
import type { RunnerSummary, TestRunResult } from '../types.js';

export function createBackendProvider(): TestProvider {
  return {
    id: '@webstir-io/webstir-testing/backend',
    async runTests(files: readonly string[]) {
      if (files.length === 0 || shouldSkipHarness()) {
        return await runVmRuntime(files);
      }

      let harness;
      try {
        harness = await createBackendTestHarness();
      } catch (error) {
        const message = formatError(error);
        console.error(`[backend-tests] ${message}`);
        return createHarnessErrorSummary(message);
      }

      try {
        setBackendTestContext(harness.context);
        return await runVmRuntime(files);
      } catch (error) {
        const message = formatError(error);
        console.error(`[backend-tests] ${message}`);
        return createHarnessErrorSummary(message);
      } finally {
        setBackendTestContext(null);
        await harness.stop().catch(() => undefined);
      }
    },
  };
}

function shouldSkipHarness(): boolean {
  const toggle = (process.env.WEBSTIR_BACKEND_TESTS ?? '').toLowerCase();
  return toggle === 'off' || toggle === 'skip' || toggle === 'false';
}

function createHarnessErrorSummary(message: string): RunnerSummary {
  const result: TestRunResult = {
    name: '[backend test harness]',
    file: 'backend-server',
    passed: false,
    message,
    durationMs: 0,
  };

  return {
    passed: 0,
    failed: 1,
    total: 1,
    durationMs: 0,
    results: [result],
  };
}

function formatError(error: unknown): string {
  if (error instanceof Error) {
    return error.message;
  }
  return typeof error === 'string' ? error : 'Backend test harness failed.';
}
