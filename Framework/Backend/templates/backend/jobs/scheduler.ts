#!/usr/bin/env node
import { setInterval } from 'node:timers';

import { loadJobs } from './runtime.js';

const args = process.argv.slice(2);

async function main() {
  if (args.includes('--help') || args.includes('-h')) {
    printHelp();
    return;
  }

  const jobs = await loadJobs();
  if (jobs.length === 0) {
    console.info('[jobs] no jobs registered in webstir.moduleManifest.jobs');
    return;
  }

  if (args.includes('--list')) {
    listJobs(jobs);
    return;
  }

  const jobName = parseOption('--job');
  const watch = args.includes('--watch');
  const runAll = args.includes('--all') || (!jobName && !watch);

  if (watch) {
    await startWatch(jobs, jobName);
    return;
  }

  if (jobName) {
    await runNamedJob(jobs, jobName);
    return;
  }

  if (runAll) {
    for (const job of jobs) {
      await runJob(job);
    }
    return;
  }
}

async function startWatch(jobs: Awaited<ReturnType<typeof loadJobs>>, jobName?: string) {
  const filtered = jobName ? jobs.filter((job) => job.name === jobName) : jobs;
  if (filtered.length === 0) {
    console.error(jobName ? `[jobs] job '${jobName}' not found` : '[jobs] no jobs available to watch');
    process.exitCode = 1;
    return;
  }

  const timers = filtered.map((job) => scheduleJob(job));
  if (timers.every((timer) => timer === undefined)) {
    console.warn('[jobs] no jobs have schedules compatible with the built-in watcher. Use an external scheduler.');
    return;
  }

  console.info('[jobs] watching jobs:', filtered.map((job) => job.name).join(', '));
  process.stdin.resume();
}

function scheduleJob(job: Awaited<ReturnType<typeof loadJobs>>[number]) {
  const intervalMs = toInterval(job.schedule);
  if (intervalMs === null) {
    console.info(
      `[jobs] schedule '${job.schedule ?? 'unspecified'}' is not supported by the built-in watcher. Run manually or use an external scheduler.`
    );
    return undefined;
  }

  if (intervalMs === 0) {
    void runJob(job);
    return undefined;
  }

  void runJob(job);
  return setInterval(() => {
    void runJob(job);
  }, intervalMs);
}

async function runNamedJob(jobs: Awaited<ReturnType<typeof loadJobs>>, jobName: string) {
  const job = jobs.find((item) => item.name === jobName);
  if (!job) {
    console.error(`[jobs] job '${jobName}' not found`);
    process.exitCode = 1;
    return;
  }
  await runJob(job);
}

async function runJob(job: Awaited<ReturnType<typeof loadJobs>>[number]) {
  const startedAt = new Date();
  console.info(`[jobs] running ${job.name} (schedule: ${job.schedule ?? 'manual'})`);
  try {
    await job.run();
    console.info(`[jobs] ${job.name} completed in ${(Date.now() - startedAt.getTime()).toFixed(0)}ms`);
  } catch (error) {
    console.error(`[jobs] ${job.name} failed: ${(error as Error).message}`);
    process.exitCode = 1;
  }
}

function listJobs(jobs: Awaited<ReturnType<typeof loadJobs>>) {
  for (const job of jobs) {
    console.info(`- ${job.name}${job.schedule ? ` (${job.schedule})` : ''}${job.description ? ` — ${job.description}` : ''}`);
  }
}

function parseOption(flag: string): string | undefined {
  const index = args.findIndex((arg) => arg === flag);
  if (index !== -1 && args[index + 1] && !args[index + 1].startsWith('-')) {
    return args[index + 1];
  }
  const prefix = `${flag}=`;
  const inline = args.find((arg) => arg.startsWith(prefix));
  if (inline) {
    return inline.slice(prefix.length);
  }
  return undefined;
}

function toInterval(schedule: string | undefined): number | null {
  if (!schedule) {
    return null;
  }
  const trimmed = schedule.trim().toLowerCase();
  if (!trimmed) return null;

  if (trimmed.startsWith('@')) {
    switch (trimmed.slice(1)) {
      case 'hourly':
        return 60 * 60 * 1000;
      case 'daily':
      case 'midnight':
        return 24 * 60 * 60 * 1000;
      case 'weekly':
        return 7 * 24 * 60 * 60 * 1000;
      case 'reboot':
        return 0;
      default:
        return null;
    }
  }

  const rateMatch = /^rate\((\d+)\s+(second|seconds|minute|minutes|hour|hours)\)$/.exec(trimmed);
  if (rateMatch) {
    const value = Number(rateMatch[1]);
    const unit = rateMatch[2];
    const multiplier =
      unit.startsWith('second') ? 1000 : unit.startsWith('minute') ? 60 * 1000 : unit.startsWith('hour') ? 60 * 60 * 1000 : 0;
    return value > 0 && multiplier > 0 ? value * multiplier : null;
  }

  return null;
}

function printHelp() {
  console.info(`Usage:
  node build/backend/jobs/scheduler.js [--list]
  node build/backend/jobs/scheduler.js --job <name>
  node build/backend/jobs/scheduler.js --watch [--job <name>]

Options:
  --list            Show registered jobs and exit
  --job <name>      Run a specific job immediately (or watch a single job)
  --all             Run all jobs once (default when no options are provided)
  --watch           Run supported jobs on an interval (supports @hourly/@daily/@weekly/@reboot and rate(...) syntax)
  --help            Display this message
`);
}

const isMain = (() => {
  try {
    const argv1 = process.argv?.[1];
    if (!argv1) return false;
    const here = new URL(import.meta.url);
    const run = new URL(`file://${argv1}`);
    return here.pathname === run.pathname;
  } catch {
    return false;
  }
})();

if (isMain) {
  main().catch((error) => {
    console.error('[jobs] scheduler failed:', error);
    process.exitCode = 1;
  });
}
