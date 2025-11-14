#!/usr/bin/env node
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

import { createDatabaseClient } from './connection.js';
import type { DatabaseClient } from './connection.js';

const args = process.argv.slice(2);
const MIGRATIONS_DIR = path.join(path.dirname(fileURLToPath(import.meta.url)), 'migrations');

export type MigrationFn = (ctx: MigrationContext) => Promise<void> | void;

interface MigrationModule {
  id: string;
  up: MigrationFn;
  down?: MigrationFn;
}

export interface MigrationContext {
  sql(query: string, params?: unknown[]): Promise<void>;
  query<T = unknown>(query: string, params?: unknown[]): Promise<T[]>;
}

async function main() {
  if (args.includes('--help') || args.includes('-h')) {
    printHelp();
    return;
  }

  const migrations = await loadMigrations();
  if (args.includes('--list')) {
    printMigrations(migrations);
    return;
  }

  if (migrations.length === 0) {
    console.warn('[migrate] No migrations found under src/backend/db/migrations');
    return;
  }

  const direction: 'up' | 'down' = args.includes('--down') ? 'down' : 'up';
  const steps = parseSteps();

  const client = await createDatabaseClient();
  try {
    await ensureMigrationsTable(client);
    if (direction === 'down') {
      await runDown(client, migrations, steps);
    } else {
      await runUp(client, migrations, steps);
    }
  } finally {
    await client.close();
  }
}

async function runUp(client: DatabaseClient, migrations: MigrationModule[], steps: number | undefined) {
  const applied = await getAppliedMigrations(client);
  const pending = migrations.filter((migration) => !applied.includes(migration.id));
  if (pending.length === 0) {
    console.info('[migrate] Database is up to date.');
    return;
  }

  const toRun = typeof steps === 'number' ? pending.slice(0, steps) : pending;
  for (const migration of toRun) {
    console.info(`[migrate] Applying ${migration.id}`);
    await migration.up(createMigrationContext(client));
    await recordMigration(client, migration.id);
  }
}

async function runDown(client: DatabaseClient, migrations: MigrationModule[], steps: number | undefined) {
  const applied = await getAppliedMigrations(client);
  if (applied.length === 0) {
    console.info('[migrate] No applied migrations to roll back.');
    return;
  }

  const toRollback = typeof steps === 'number' ? applied.slice(-steps) : applied;
  const migrationMap = new Map(migrations.map((migration) => [migration.id, migration]));

  for (const id of toRollback.reverse()) {
    const migration = migrationMap.get(id);
    if (!migration?.down) {
      console.warn(`[migrate] Skipping ${id} (no down() function exported).`);
      continue;
    }
    console.info(`[migrate] Reverting ${id}`);
    await migration.down(createMigrationContext(client));
    await deleteMigrationRecord(client, id);
  }
}

async function ensureMigrationsTable(client: DatabaseClient) {
  const table = process.env.DATABASE_MIGRATIONS_TABLE ?? '_webstir_migrations';
  await client.execute(
    `CREATE TABLE IF NOT EXISTS ${table} (
      id TEXT PRIMARY KEY,
      applied_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
    )`
  );
}

async function getAppliedMigrations(client: DatabaseClient): Promise<string[]> {
  const table = process.env.DATABASE_MIGRATIONS_TABLE ?? '_webstir_migrations';
  const rows = await client.query<{ id: string }>(`SELECT id FROM ${table} ORDER BY applied_at`);
  return rows.map((row) => row.id);
}

async function recordMigration(client: DatabaseClient, id: string) {
  const table = process.env.DATABASE_MIGRATIONS_TABLE ?? '_webstir_migrations';
  await client.execute(`INSERT INTO ${table} (id) VALUES (?)`, [id]);
}

async function deleteMigrationRecord(client: DatabaseClient, id: string) {
  const table = process.env.DATABASE_MIGRATIONS_TABLE ?? '_webstir_migrations';
  await client.execute(`DELETE FROM ${table} WHERE id = ?`, [id]);
}

function createMigrationContext(client: DatabaseClient): MigrationContext {
  return {
    sql: (query, params) => client.execute(query, params),
    query: (query, params) => client.query(query, params)
  };
}

async function loadMigrations(): Promise<MigrationModule[]> {
  try {
    const files = await fs.readdir(MIGRATIONS_DIR);
    const scriptFiles = files
      .filter((file) => /\.[cm]?[jt]s$/.test(file))
      .sort();
    const modules: MigrationModule[] = [];
    for (const file of scriptFiles) {
      const moduleUrl = pathToFileURL(path.join(MIGRATIONS_DIR, file)).href + `?t=${Date.now()}`;
      const imported = (await import(moduleUrl)) as Record<string, unknown>;
      const migration = normalizeMigrationModule(imported, file);
      if (migration) {
        modules.push(migration);
      }
    }
    return modules;
  } catch (error) {
    console.error('[migrate] Failed to load migrations:', (error as Error).message);
    return [];
  }
}

function normalizeMigrationModule(exports: Record<string, unknown>, file: string): MigrationModule | undefined {
  const id =
    typeof exports.id === 'string'
      ? exports.id
      : typeof exports.default === 'object' && exports.default && typeof (exports.default as any).id === 'string'
        ? (exports.default as any).id
        : path.basename(file).replace(/\.[cm]?[jt]s$/, '');
  const up: MigrationFn | undefined =
    typeof exports.up === 'function'
      ? (exports.up as MigrationFn)
      : exports.default && typeof (exports.default as any).up === 'function'
        ? ((exports.default as any).up as MigrationFn)
        : undefined;
  if (!up) {
    console.warn(`[migrate] ${file} does not export an up() function. Skipping.`);
    return undefined;
  }
  const down: MigrationFn | undefined =
    typeof exports.down === 'function'
      ? (exports.down as MigrationFn)
      : exports.default && typeof (exports.default as any).down === 'function'
        ? ((exports.default as any).down as MigrationFn)
        : undefined;
  return { id, up, down };
}

function parseSteps(): number | undefined {
  const value = parseOption('--steps');
  if (!value) return undefined;
  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed <= 0) {
    throw new Error(`--steps must be a positive integer (received "${value}")`);
  }
  return Math.floor(parsed);
}

function parseOption(flag: string): string | undefined {
  const index = args.indexOf(flag);
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

function printMigrations(migrations: MigrationModule[]) {
  if (migrations.length === 0) {
    console.info('[migrate] No migrations found.');
    return;
  }
  console.info('[migrate] Available migrations:');
  for (const migration of migrations) {
    console.info(`- ${migration.id}${migration.down ? '' : ' (no down)'}`);
  }
}

function printHelp() {
  console.info(`Usage:
  npx tsx src/backend/db/migrate.ts [--list]
  npx tsx src/backend/db/migrate.ts --down [--steps 1]

Options:
  --list        Show migrations and exit
  --down        Roll back migrations instead of applying new ones
  --steps <n>   Limit how many migrations to run in the current direction
  --help        Show this message

Notes:
  - Defaults to reading migration files from src/backend/db/migrations.
  - DATABASE_URL controls the target database (file:./dev.sqlite by default).
  - Install 'better-sqlite3' for SQLite or 'pg' for Postgres before running.`);
}

main().catch((error) => {
  console.error('[migrate] Failed:', error);
  process.exitCode = 1;
});
