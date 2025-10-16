#!/usr/bin/env node
import console from 'node:console';
import path from 'node:path';
import process from 'node:process';

const STDOUT_PREFIX = 'WEBSTIR_MODULE_RESULT ';
const STDERR_PREFIX = 'WEBSTIR_MODULE_EVENT ';

overrideConsole();

const args = parseArgs(process.argv.slice(2));

try {
    validateArgs(args);

    process.chdir(args.workspace);

    const providerModule = await import(args.provider);
    const provider = resolveProvider(providerModule, args.provider);

    const envMap = parseEnv(args.env);
    const mode = typeof args.mode === 'string' ? args.mode.toLowerCase() : 'build';
    envMap.WEBSTIR_MODULE_MODE = mode;

    const buildResult = await provider.build({
        workspaceRoot: args.workspace,
        env: envMap,
        incremental: args.incremental === 'true'
    });

    const output = {
        provider: provider.metadata,
        manifest: buildResult.manifest,
        artifacts: buildResult.artifacts ?? []
    };

    process.stdout.write(`${STDOUT_PREFIX}${JSON.stringify(output)}\n`);
} catch (error) {
    const message = error instanceof Error ? error.stack ?? error.message : String(error);
    console.error(message);
    process.exitCode = 1;
}

function parseArgs(argv) {
    const result = {
        env: []
    };

    for (let index = 0; index < argv.length; index += 1) {
        const value = argv[index];
        switch (value) {
            case '--provider':
                result.provider = argv[++index];
                break;
            case '--workspace':
                result.workspace = argv[++index];
                break;
            case '--mode':
                result.mode = argv[++index];
                break;
            case '--env':
                result.env.push(argv[++index]);
                break;
            case '--incremental':
                result.incremental = argv[++index];
                break;
            default:
                break;
        }
    }

    return result;
}

function parseEnv(envArgs) {
    const map = {};

    for (const entry of envArgs ?? []) {
        if (typeof entry !== 'string') {
            continue;
        }

        const separatorIndex = entry.indexOf('=');
        if (separatorIndex < 0) {
            map[entry] = undefined;
            continue;
        }

        const key = entry.slice(0, separatorIndex);
        const value = entry.slice(separatorIndex + 1);
        map[key] = value;
    }

    return map;
}

function resolveProvider(module, providerId) {
    const exports = [];

    if (module?.default?.metadata) {
        exports.push(module.default);
    }

    for (const value of Object.values(module)) {
        if (value && typeof value === 'object' && 'metadata' in value) {
            exports.push(value);
        }
    }

    if (exports.length === 0) {
        throw new Error(`No module provider exported by ${providerId}.`);
    }

    return exports[0];
}

function validateArgs(args) {
    if (!args.provider) {
        throw new Error('Missing required --provider <package> argument.');
    }

    if (!args.workspace) {
        throw new Error('Missing required --workspace <path> argument.');
    }

    if (!path.isAbsolute(args.workspace)) {
        throw new Error(`Workspace must be an absolute path (received: ${args.workspace}).`);
    }
}

function overrideConsole() {
    const stdoutConsole = new console.Console(process.stdout, process.stdout);
    const stderrConsole = new console.Console(process.stderr, process.stderr);

    const forward = (level, originalConsole) => (...values) => {
        const message = values.map((value) => stringify(value)).join(' ');
        const payload = {
            type: level,
            message
        };
        process.stderr.write(`${STDERR_PREFIX}${JSON.stringify(payload)}\n`);
        originalConsole[level](...values);
    };

    console.log = forward('log', stdoutConsole);
    console.info = forward('info', stdoutConsole);
    console.warn = forward('warn', stderrConsole);
    console.error = forward('error', stderrConsole);
}

function stringify(value) {
    if (typeof value === 'string') {
        return value;
    }

    try {
        return JSON.stringify(value);
    } catch {
        return String(value);
    }
}
