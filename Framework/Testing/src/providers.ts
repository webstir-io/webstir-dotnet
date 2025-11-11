import { run as runVmRuntime } from './runtime.js';
import { createBackendProvider } from './providers/backend-provider.js';
import type { RunnerSummary, TestRuntime } from './types.js';

export interface TestProvider {
  readonly id: string;
  runTests(files: readonly string[]): Promise<RunnerSummary>;
}

export interface ProviderRegistry {
  get(runtime: TestRuntime): TestProvider | null;
}

export function createDefaultProviderRegistry(): ProviderRegistry {
  const defaultProvider: TestProvider = {
    id: '@webstir-io/webstir-testing/default',
    runTests: runVmRuntime,
  };

  const providers = new Map<TestRuntime, TestProvider>([
    ['frontend', defaultProvider],
    ['backend', createBackendProvider()],
  ]);

  return {
    get(runtime: TestRuntime): TestProvider | null {
      return providers.get(runtime) ?? null;
    },
  };
}

export function createProviderRegistry(): ProviderRegistry {
  return createDefaultProviderRegistry();
}
