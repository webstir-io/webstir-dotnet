export type TestCallback = () => unknown | Promise<unknown>;

export interface RegisteredTest {
  readonly name: string;
  readonly fn: TestCallback;
}

export interface TestRunResult {
  readonly name: string;
  readonly file: string;
  readonly passed: boolean;
  readonly message: string | null;
  readonly durationMs: number;
}

export interface RunnerSummary {
  readonly passed: number;
  readonly failed: number;
  readonly total: number;
  readonly durationMs: number;
  readonly results: readonly TestRunResult[];
}
