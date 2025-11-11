import path from 'node:path';

import { discoverTestManifest } from '../discovery.js';
import { emitEvent, createRunId } from '../events.js';
import { executeRun } from '../execution.js';
import { applyRuntimeFilter, describeRuntimeFilter, normalizeRuntimeFilter } from '../runtime-filter.js';
import type { RunnerLogEvent, RunnerSummary, RunnerSummaryEvent } from '../types.js';

export interface TestCommandOptions {
  readonly workspace: string;
}

export async function runTestCommand(options: TestCommandOptions): Promise<void> {
  const runId = createRunId();
  const workspaceRoot = path.resolve(options.workspace);
  const runtimeFilter = normalizeRuntimeFilter(process.env.WEBSTIR_TEST_RUNTIME);

  try {
    const manifest = await discoverTestManifest(workspaceRoot);
    const filteredManifest = applyRuntimeFilter(manifest, runtimeFilter);
    const filterMessage = describeRuntimeFilter(runtimeFilter, manifest.modules.length, filteredManifest.modules.length);
    if (filterMessage) {
      emitEvent({
        type: 'log',
        runId,
        level: 'info',
        message: filterMessage,
      });
    }
    emitEvent({
      type: 'start',
      runId,
      manifest: filteredManifest,
    });

    if (filteredManifest.modules.length === 0) {
      const noTests: RunnerLogEvent = {
        type: 'log',
        runId,
        level: 'info',
        message: 'No tests found under src/**/tests/.',
      };
      emitEvent(noTests);
      emitEvent(makeOverallSummary(runId));
      return;
    }

    const overall = await executeRun(runId, filteredManifest);
    emitEvent(makeOverallSummary(runId, overall));

    if (overall.failed > 0) {
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
}

function makeOverallSummary(runId: string, summary: RunnerSummary = emptySummary): RunnerSummaryEvent {
  return {
    type: 'summary',
    runId,
    runtime: 'all',
    summary,
  } satisfies RunnerSummaryEvent;
}

const emptySummary: RunnerSummary = {
  passed: 0,
  failed: 0,
  total: 0,
  durationMs: 0,
  results: [],
};
