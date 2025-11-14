import path from 'node:path';
import { mkdirSync } from 'node:fs';

export interface DatabaseClient {
  query<T = unknown>(sql: string, params?: unknown[]): Promise<T[]>;
  execute(sql: string, params?: unknown[]): Promise<void>;
  close(): Promise<void>;
}

export async function createDatabaseClient(url = process.env.DATABASE_URL ?? 'file:./data/dev.sqlite'): Promise<DatabaseClient> {
  if (isSqlite(url)) {
    return createSqliteClient(url);
  }
  if (isPostgres(url)) {
    return createPostgresClient(url);
  }
  throw new Error(
    `[db] Unsupported DATABASE_URL '${url}'. Use file:./path/to.sqlite or postgres://...`
  );
}

function isSqlite(url: string): boolean {
  return url.startsWith('file:') || url.endsWith('.sqlite') || url.endsWith('.db');
}

function isPostgres(url: string): boolean {
  return url.startsWith('postgres://') || url.startsWith('postgresql://');
}

async function createSqliteClient(url: string): Promise<DatabaseClient> {
  let Database: typeof import('better-sqlite3');
  try {
    const sqliteModule = await import('better-sqlite3');
    Database = sqliteModule.default ?? (sqliteModule as unknown as typeof import('better-sqlite3'));
  } catch (error) {
    throw new Error(
      `[db] Failed to load better-sqlite3. Install it in your workspace with "npm install better-sqlite3". (${(error as Error).message})`
    );
  }

  const target = normalizeSqlitePath(url);
  mkdirSync(path.dirname(target), { recursive: true });
  const db = new Database(target);

  return {
    async query(sql, params) {
      const statement = db.prepare(sql);
      return statement.all(params ?? []);
    },
    async execute(sql, params) {
      const statement = db.prepare(sql);
      statement.run(params ?? []);
    },
    async close() {
      db.close();
    }
  };
}

async function createPostgresClient(url: string): Promise<DatabaseClient> {
  type PgClientCtor = new (...args: any[]) => {
    query: (text: string, params?: unknown[]) => Promise<{ rows: any[] }>;
    connect: () => Promise<void>;
    end: () => Promise<void>;
  };

  let ClientCtor: PgClientCtor;
  try {
    const pgModule = await import('pg');
    ClientCtor = (pgModule as unknown as { Client: PgClientCtor }).Client;
  } catch (error) {
    throw new Error(
      `[db] Failed to load pg. Install it in your workspace with "npm install pg". (${(error as Error).message})`
    );
  }

  const client = new ClientCtor({ connectionString: url });
  await client.connect();

  return {
    async query(sql, params) {
      const result = await client.query(sql, params);
      return result.rows;
    },
    async execute(sql, params) {
      await client.query(sql, params);
    },
    async close() {
      await client.end();
    }
  };
}

function normalizeSqlitePath(url: string): string {
  if (url.startsWith('file:')) {
    return path.resolve(url.slice('file:'.length));
  }
  return path.resolve(url);
}
