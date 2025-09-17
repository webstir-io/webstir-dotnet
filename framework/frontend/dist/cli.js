#!/usr/bin/env node
"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
const commander_1 = require("commander");
const operations_js_1 = require("./operations.js");
const program = new commander_1.Command();
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
        await (0, operations_js_1.runBuild)({
            workspaceRoot: cmd.workspace,
            changedFile: cmd.changedFile ?? undefined
        });
    }
    catch (error) {
        handleError(error);
    }
});
program
    .command('publish')
    .description('Build production assets into the dist directory')
    .requiredOption('-w, --workspace <path>', 'Absolute path to the workspace root')
    .action(async (cmd) => {
    try {
        await (0, operations_js_1.runPublish)({ workspaceRoot: cmd.workspace });
    }
    catch (error) {
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
        await (0, operations_js_1.runRebuild)({
            workspaceRoot: cmd.workspace,
            changedFile: cmd.changedFile ?? undefined
        });
    }
    catch (error) {
        handleError(error);
    }
});
program.parseAsync(process.argv).catch(handleError);
function handleError(error) {
    if (error instanceof Error) {
        console.error(error.message);
    }
    else {
        console.error('Unknown error', error);
    }
    process.exitCode = 1;
}
