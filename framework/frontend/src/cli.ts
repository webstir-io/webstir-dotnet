#!/usr/bin/env node
import { Command } from 'commander';
import { runAddPage, runBuild, runPublish, runRebuild } from './operations.js';
import { WatchDaemon } from './watch/watchDaemon.js';

const program = new Command();

program
    .name('webstir-frontend')
    .description('Webstir frontend build orchestrator');

program
    .command('build')
    .description('Build frontend assets for development workflows')
    .requiredOption('-w, --workspace <path>', 'Absolute path to the workspace root')
    .option('-c, --changed-file <path>', 'Optional path filter for incremental builds')
    .action(async (cmd) => {
        try {
            await runBuild({
                workspaceRoot: cmd.workspace,
                changedFile: cmd.changedFile ?? undefined
            });
        } catch (error) {
            handleError(error);
        }
    });

program
    .command('publish')
    .description('Build production assets into the dist directory')
    .requiredOption('-w, --workspace <path>', 'Absolute path to the workspace root')
    .action(async (cmd) => {
        try {
            await runPublish({ workspaceRoot: cmd.workspace });
        } catch (error) {
            handleError(error);
        }
    });

program
    .command('rebuild')
    .description('Rebuild frontend assets in response to file changes')
    .requiredOption('-w, --workspace <path>', 'Absolute path to the workspace root')
    .requiredOption('-c, --changed-file <path>', 'Path to the changed file triggering the rebuild')
    .action(async (cmd) => {
        try {
            await runRebuild({
                workspaceRoot: cmd.workspace,
                changedFile: cmd.changedFile ?? undefined
            });
        } catch (error) {
            handleError(error);
        }
    });

program
    .command('add-page <name>')
    .description('Scaffold a new frontend page (HTML/CSS/TS)')
    .requiredOption('-w, --workspace <path>', 'Absolute path to the workspace root')
    .action(async (name, cmd) => {
        try {
            await runAddPage({
                workspaceRoot: cmd.workspace,
                pageName: name
            });
        } catch (error) {
            handleError(error);
        }
    });

program
    .command('watch-daemon')
    .description('Run the persistent frontend watch daemon')
    .requiredOption('-w, --workspace <path>', 'Absolute path to the workspace root')
    .option('--no-auto-start', 'Defer startup until a start command is received')
    .option('-v, --verbose', 'Enable verbose watch diagnostics')
    .option('--hmr-verbose', 'Log detailed hot-update diagnostics')
    .action(async (cmd) => {
        try {
            const daemon = new WatchDaemon({
                workspaceRoot: cmd.workspace,
                autoStart: cmd.autoStart,
                verbose: cmd.verbose === true,
                hmrVerbose: cmd.hmrVerbose === true
            });
            await daemon.run();
        } catch (error) {
            handleError(error);
        }
    });

program.parseAsync(process.argv).catch(handleError);

function handleError(error: unknown): void {
    if (error instanceof Error) {
        console.error(error.message);
    } else {
        console.error('Unknown error', error);
    }
    process.exitCode = 1;
}
