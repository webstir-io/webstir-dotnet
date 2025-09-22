import path from 'node:path';

import { emitEvent } from './events.js';
import { run as runFrontendRuntime } from './runtime.js';
import type {
  RunnerLogEvent,
  RunnerResultEvent,
  RunnerSummary,
  RunnerSummaryEvent,
  TestManifest,
  TestModule,
  TestRunResult,
  TestRuntime,
} from './types.js';

export async function executeRun(runId: string, manifest: TestManifest): Promise<RunnerSummary> {
  let accumulator = createEmptySummary();
  const byRuntime = groupModulesByRuntime(manifest.modules);

  for (const [runtime, modules] of byRuntime) {
    if (runtime === 'frontend' || runtime === 'backend') {
      const summary = await runNodeModules(runId, runtime, modules);
      accumulator = mergeSummaries(accumulator, summary);
      continue;
    }

    const skipped: RunnerLogEvent = {
      type: 'log',
      runId,
      level: 'warn',
      message: `Skipping ${modules.length} test${modules.length === 1 ? '' : 's'} for unsupported runtime '${runtime}'.`,
    };
    emitEvent(skipped);
  }

  return accumulator;
}

function groupModulesByRuntime(modules: readonly TestModule[]): Map<TestRuntime, TestModule[]> {
  const result = new Map<TestRuntime, TestModule[]>();
  for (const module of modules) {
    const list = result.get(module.runtime);
    if (list) {
      list.push(module);
    } else {
      result.set(module.runtime, [module]);
    }
  }

  return result;
}

async function runNodeModules(runId: string, runtime: TestRuntime, modules: readonly TestModule[]): Promise<RunnerSummary> {
  const files: string[] = [];
  const moduleByPath = new Map<string, TestModule>();

  for (const module of modules) {
    if (!module.compiledPath) {
      emitEvent({
        type: 'log',
        runId,
        level: 'warn',
        message: `Test ${module.id} has no compiled output; skipping.`,
      });
      continue;
    }

    const absolute = path.resolve(module.compiledPath);
    moduleByPath.set(absolute, module);
    files.push(absolute);
  }

  if (files.length === 0) {
    const emptySummary = createEmptySummary();
    emitEvent(makeSummaryEvent(runId, runtime, emptySummary));
    return emptySummary;
  }

  const summary = await runFrontendRuntime(files);
  for (const result of summary.results) {
    const absolute = path.resolve(result.file);
    const module = moduleByPath.get(absolute);
    const event: RunnerResultEvent = {
      type: 'result',
      runId,
      runtime,
      moduleId: module?.id ?? absolute,
      result,
    };
    emitEvent(event);
  }

  emitEvent(makeSummaryEvent(runId, runtime, summary));
  return summary;
}

function createEmptySummary(): RunnerSummary {
  return {
    passed: 0,
    failed: 0,
    total: 0,
    durationMs: 0,
    results: [],
  } satisfies RunnerSummary;
}

function mergeSummaries(left: RunnerSummary, right: RunnerSummary): RunnerSummary {
  return {
    passed: left.passed + right.passed,
    failed: left.failed + right.failed,
    total: left.total + right.total,
    durationMs: left.durationMs + right.durationMs,
    results: [...left.results, ...right.results],
  } satisfies RunnerSummary;
}

function makeSummaryEvent(runId: string, runtime: TestRuntime, summary: RunnerSummary): RunnerSummaryEvent {
  return {
    type: 'summary',
    runId,
    runtime,
    summary,
  } satisfies RunnerSummaryEvent;
}
