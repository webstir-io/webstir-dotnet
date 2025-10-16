import type {
  RunnerErrorEvent,
  RunnerEvent,
  RunnerLogEvent,
  RunnerResultEvent,
  RunnerStartEvent,
  RunnerSummary,
  RunnerSummaryEvent,
  RunnerWatchIterationEvent,
  TestManifest,
  TestModule,
  TestRunResult,
  TestRuntime,
} from '@webstir-io/testing-contract';

export type {
  RunnerErrorEvent,
  RunnerEvent,
  RunnerLogEvent,
  RunnerResultEvent,
  RunnerStartEvent,
  RunnerSummary,
  RunnerSummaryEvent,
  RunnerWatchIterationEvent,
  TestManifest,
  TestModule,
  TestRunResult,
  TestRuntime,
} from '@webstir-io/testing-contract';

export type TestCallback = () => unknown | Promise<unknown>;

export interface RegisteredTest {
  readonly name: string;
  readonly fn: TestCallback;
}
