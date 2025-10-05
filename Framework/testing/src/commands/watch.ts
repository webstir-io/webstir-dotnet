import path from 'node:path';
import chokidar from 'chokidar';

import { discoverTestManifest } from '../discovery.js';
import { emitEvent, createRunId } from '../events.js';
import { executeRun } from '../execution.js';
import type { RunnerSummary, RunnerSummaryEvent, RunnerWatchIterationEvent } from '../types.js';

export interface WatchCommandOptions {
  readonly workspace: string;
  readonly debounceMs?: number;
}

export async function runWatchCommand(options: WatchCommandOptions): Promise<void> {
  const sessionId = createRunId();
  const workspaceRoot = path.resolve(options.workspace);
  const srcRoot = path.join(workspaceRoot, 'src');
  const debounce = options.debounceMs ?? 150;
  let iteration = 0;
  let pending = Promise.resolve();
  let scheduled: NodeJS.Timeout | null = null;
  let queuedPaths: string[] = [];
  let exitCode = 0;

  const runIteration = async (changedFiles: readonly string[]): Promise<void> => {
    iteration += 1;
    const iterationId = `${sessionId}-${iteration}`;
    emitEvent(makeWatchEvent(sessionId, iteration, 'start', changedFiles));

    try {
      const manifest = await discoverTestManifest(workspaceRoot);
      emitEvent({
        type: 'start',
        runId: iterationId,
        manifest,
      });

      if (manifest.modules.length === 0) {
        emitEvent({
          type: 'summary',
          runId: iterationId,
          runtime: 'all',
          summary: emptySummary,
        } satisfies RunnerSummaryEvent);
        emitEvent(makeWatchEvent(sessionId, iteration, 'complete', changedFiles, emptySummary));
        return;
      }

      const summary = await executeRun(iterationId, manifest);
      emitEvent({
        type: 'summary',
        runId: iterationId,
        runtime: 'all',
        summary,
      } satisfies RunnerSummaryEvent);
      emitEvent(makeWatchEvent(sessionId, iteration, 'complete', changedFiles, summary));

      if (summary.failed > 0) {
        exitCode = 1;
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      const stack = error instanceof Error ? error.stack ?? undefined : undefined;
      emitEvent({
        type: 'error',
        runId: iterationId,
        message,
        stack,
      });
      emitEvent(makeWatchEvent(sessionId, iteration, 'complete', changedFiles));
      exitCode = 1;
    }
  };

  const flushQueue = (): void => {
    const files = queuedPaths;
    queuedPaths = [];
    scheduled = null;
    pending = pending.then(async () => {
      await runIteration(files);
    }).catch((error) => {
      const message = error instanceof Error ? error.message : String(error);
      emitEvent({
        type: 'error',
        runId: sessionId,
        message,
      });
      exitCode = 1;
    });
  };

  const scheduleRun = (): void => {
    if (scheduled) {
      return;
    }
    scheduled = setTimeout(flushQueue, debounce);
  };

  const addChangedPath = (filePath: string): void => {
    const relative = path.relative(workspaceRoot, filePath);
    if (!queuedPaths.includes(relative)) {
      queuedPaths.push(relative);
    }
    scheduleRun();
  };

  const watcher = chokidar.watch(srcRoot, {
    ignoreInitial: true,
    ignored: [/(^|\/)\../, '**/node_modules/**', '**/build/**', '**/dist/**'],
  });

  watcher.on('add', addChangedPath);
  watcher.on('change', addChangedPath);
  watcher.on('unlink', addChangedPath);

  const shutdown = async (): Promise<void> => {
    if (scheduled) {
      clearTimeout(scheduled);
    }

    await watcher.close();
    await pending;

    if (exitCode > 0) {
      process.exitCode = exitCode;
    }
  };

  process.on('SIGINT', async () => {
    await shutdown();
    process.exit();
  });
  process.on('SIGTERM', async () => {
    await shutdown();
    process.exit();
  });

  await runIteration([]);
}

const emptySummary: RunnerSummary = {
  passed: 0,
  failed: 0,
  total: 0,
  durationMs: 0,
  results: [],
};

function makeWatchEvent(
  runId: string,
  iteration: number,
  phase: 'start' | 'complete',
  changedFiles: readonly string[],
  summary?: RunnerSummary,
): RunnerWatchIterationEvent {
  return {
    type: 'watch-iteration',
    runId,
    iteration,
    phase,
    changedFiles,
    summary,
  } satisfies RunnerWatchIterationEvent;
}
