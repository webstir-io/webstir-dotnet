#!/usr/bin/env node
import { run } from './runtime.js';
import type { RunnerSummary } from './types.js';

async function readInput(): Promise<string> {
  const chunks: Buffer[] = [];
  for await (const chunk of process.stdin) {
    if (typeof chunk === 'string') {
      chunks.push(Buffer.from(chunk));
    } else {
      chunks.push(chunk);
    }
  }

  if (chunks.length === 0) {
    return '';
  }

  return Buffer.concat(chunks).toString('utf8');
}

function coerceFiles(value: unknown): string[] {
  if (!Array.isArray(value)) {
    return [];
  }

  return value.map((entry) => String(entry));
}

async function main(): Promise<void> {
  let raw = await readInput();
  if (!raw) {
    raw = '[]';
  }

  let files: string[] = [];
  try {
    files = coerceFiles(JSON.parse(raw));
  } catch {
    files = [];
  }

  try {
    const summary = await run(files);
    writeSummary(summary);
  } catch (error) {
    const message = error instanceof Error ? error.stack ?? error.message : String(error);
    writeSummary({
      passed: 0,
      failed: files.length > 0 ? files.length : 1,
      total: files.length > 0 ? files.length : 1,
      durationMs: 0,
      results: [
        {
          name: '[runner error]',
          file: '',
          passed: false,
          message,
          durationMs: 0,
        },
      ],
    });
    process.exitCode = 1;
  }
}

function writeSummary(summary: RunnerSummary): void {
  try {
    process.stdout.write(JSON.stringify(summary));
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    const fallback: RunnerSummary = {
      passed: 0,
      failed: summary.total,
      total: summary.total,
      durationMs: summary.durationMs,
      results: summary.results,
    };

    process.stdout.write(JSON.stringify({
      ...fallback,
      results: [
        ...fallback.results,
        {
          name: '[runner serialization error]',
          file: '',
          passed: false,
          message,
          durationMs: 0,
        },
      ],
    } satisfies RunnerSummary));
  }
}

void main().catch((error) => {
  const message = error instanceof Error ? error.stack ?? error.message : String(error);
  writeSummary({
    passed: 0,
    failed: 1,
    total: 1,
    durationMs: 0,
    results: [
      {
        name: '[runner fatal error]',
        file: '',
        passed: false,
        message,
        durationMs: 0,
      },
    ],
  });
  process.exitCode = 1;
});
