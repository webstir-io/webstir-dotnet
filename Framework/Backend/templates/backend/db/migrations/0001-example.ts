import type { MigrationContext } from '../migrate.js';

export const id = '0001_init';

export async function up(ctx: MigrationContext): Promise<void> {
  await ctx.sql(`
    CREATE TABLE IF NOT EXISTS example_records (
      id TEXT PRIMARY KEY,
      created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
      payload TEXT NOT NULL
    )
  `);
}

export async function down(ctx: MigrationContext): Promise<void> {
  await ctx.sql(`DROP TABLE IF EXISTS example_records`);
}
