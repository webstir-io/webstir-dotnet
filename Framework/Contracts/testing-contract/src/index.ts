export type TestRuntime = 'frontend' | 'backend';

export interface TestModule {
  readonly id: string;
  readonly runtime: TestRuntime;
  readonly sourcePath: string;
  readonly compiledPath: string | null;
}

export interface TestManifest {
  readonly workspaceRoot: string;
  readonly generatedAt: string;
  readonly modules: readonly TestModule[];
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

export interface RunnerStartEvent {
  readonly type: 'start';
  readonly runId: string;
  readonly manifest: TestManifest;
}

export interface RunnerResultEvent {
  readonly type: 'result';
  readonly runId: string;
  readonly runtime: TestRuntime;
  readonly moduleId: string;
  readonly result: TestRunResult;
}

export interface RunnerSummaryEvent {
  readonly type: 'summary';
  readonly runId: string;
  readonly runtime: TestRuntime | 'all';
  readonly summary: RunnerSummary;
}

export interface RunnerLogEvent {
  readonly type: 'log';
  readonly runId: string;
  readonly level: 'info' | 'warn' | 'error';
  readonly message: string;
}

export interface RunnerErrorEvent {
  readonly type: 'error';
  readonly runId: string;
  readonly message: string;
  readonly stack?: string;
}

export interface RunnerWatchIterationEvent {
  readonly type: 'watch-iteration';
  readonly runId: string;
  readonly iteration: number;
  readonly phase: 'start' | 'complete';
  readonly changedFiles: readonly string[];
  readonly summary?: RunnerSummary;
}

export type RunnerEvent =
  | RunnerStartEvent
  | RunnerResultEvent
  | RunnerSummaryEvent
  | RunnerLogEvent
  | RunnerErrorEvent
  | RunnerWatchIterationEvent;
