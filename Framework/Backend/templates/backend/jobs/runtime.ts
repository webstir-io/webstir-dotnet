import { readFile } from 'node:fs/promises';
import path from 'node:path';

import { resolveWorkspaceRoot, loadEnv } from '../env.js';

export interface ManifestJobMetadata {
  readonly name: string;
  readonly schedule?: string;
  readonly description?: string;
  readonly priority?: number | string;
}

export interface RegisteredJob extends ManifestJobMetadata {
  run(): Promise<void>;
}

export async function loadJobs(): Promise<RegisteredJob[]> {
  await ensureEnvLoaded();
  const manifestJobs = await readManifestJobs();
  return manifestJobs.map((job) => ({
    ...job,
    async run() {
      const runner = await loadJobRunner(job.name);
      await runner();
    }
  }));
}

async function ensureEnvLoaded(): Promise<void> {
  try {
    loadEnv();
  } catch {
    // env loading is best-effort for job runs
  }
}

async function readManifestJobs(): Promise<ManifestJobMetadata[]> {
  const root = resolveWorkspaceRoot();
  const pkgPath = path.join(root, 'package.json');
  try {
    const raw = await readFile(pkgPath, 'utf8');
    const pkg = JSON.parse(raw) as Record<string, any>;
    const jobs = pkg?.webstir?.module?.jobs;
    if (!Array.isArray(jobs)) {
      return [];
    }
    return jobs
      .map((job) => normalizeManifestJob(job))
      .filter((job): job is ManifestJobMetadata => job !== undefined);
  } catch (error) {
    console.warn('[jobs] unable to read package.json for job metadata:', (error as Error).message);
    return [];
  }
}

function normalizeManifestJob(job: unknown): ManifestJobMetadata | undefined {
  if (!job || typeof job !== 'object') return undefined;
  const record = job as Record<string, unknown>;
  const name = typeof record.name === 'string' ? record.name.trim() : '';
  if (!name) return undefined;
  const schedule = typeof record.schedule === 'string' ? record.schedule : undefined;
  const description = typeof record.description === 'string' ? record.description : undefined;
  const priority =
    typeof record.priority === 'number' || typeof record.priority === 'string' ? record.priority : undefined;
  return { name, schedule, description, priority };
}

async function loadJobRunner(jobName: string): Promise<() => Promise<void>> {
  const candidates = buildJobModuleCandidates(jobName);
  let lastError: unknown;
  for (const candidate of candidates) {
    try {
      const imported = await import(candidate);
      const fn = (imported.run ?? imported.default) as (() => Promise<void>) | undefined;
      if (typeof fn === 'function') {
        return fn;
      }
    } catch (error) {
      lastError = error;
    }
  }
  const reason = lastError instanceof Error ? lastError.message : 'unknown';
  throw new Error(`[jobs] unable to load job '${jobName}': ${reason}`);
}

function buildJobModuleCandidates(jobName: string): string[] {
  const normalized = normalizeJobSpecifier(jobName);
  const relPaths = [
    `./${normalized}/index.js`,
    `./${normalized}/index.mjs`,
    `./${normalized}/index.ts`,
    `./${normalized}/index.mts`
  ];
  return relPaths.map((rel) => new URL(rel, import.meta.url).href + `?t=${Date.now()}`);
}

function normalizeJobSpecifier(name: string): string {
  return name
    .replace(/\\/g, '/')
    .replace(/\.\./g, '')
    .replace(/^\//, '')
    .replace(/\/$/, '');
}
