#!/usr/bin/env node
import { Command, Option } from 'commander';

import { runTestCommand } from './commands/test.js';
import { runWatchCommand } from './commands/watch.js';

const program = new Command();

program
  .name('webstir-test')
  .description('Unified test runner for Webstir workspaces');

program
  .command('test', { isDefault: true })
  .description('Run all tests for the workspace')
  .addOption(workspaceOption())
  .action(async (options) => {
    await runTestCommand({
      workspace: options.workspace,
    });
  });

program
  .command('watch')
  .description('Run tests in watch mode, re-running after file changes')
  .addOption(workspaceOption())
  .addOption(new Option('-d, --debounce <ms>', 'Debounce duration between runs (ms)').argParser(parseInteger).default(150))
  .action(async (options) => {
    await runWatchCommand({
      workspace: options.workspace,
      debounceMs: options.debounce,
    });
  });

program.parseAsync(process.argv).catch((error) => {
  const message = error instanceof Error ? error.message : String(error);
  console.error(message);
  process.exitCode = 1;
});

function workspaceOption(): Option {
  return new Option('-w, --workspace <path>', 'Absolute path to the workspace root').default(process.cwd());
}

function parseInteger(value: string): number {
  const parsed = Number.parseInt(value, 10);
  if (Number.isNaN(parsed) || parsed < 0) {
    throw new Error(`Invalid debounce value: ${value}`);
  }
  return parsed;
}
