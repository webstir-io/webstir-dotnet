import { spawn } from 'node:child_process';
import { once } from 'node:events';
import { existsSync } from 'node:fs';
import { readFile } from 'node:fs/promises';
import net from 'node:net';
import path from 'node:path';
import { getBackendTestContext, setBackendTestContext } from './context.js';
const DEFAULT_PORT = 4100;
const DEFAULT_READY_TEXT = 'API server running';
const DEFAULT_READY_TIMEOUT_MS = 15_000;
export { getBackendTestContext, setBackendTestContext };
export async function createBackendTestHarness(options = {}) {
    const workspaceRoot = options.workspaceRoot ?? process.env.WEBSTIR_WORKSPACE_ROOT ?? process.cwd();
    const buildRoot = options.buildRoot ?? process.env.WEBSTIR_BACKEND_BUILD_ROOT ?? path.join(workspaceRoot, 'build', 'backend');
    const entry = options.entry ?? process.env.WEBSTIR_BACKEND_TEST_ENTRY ?? path.join(buildRoot, 'index.js');
    const manifestPath = options.manifestPath ??
        process.env.WEBSTIR_BACKEND_TEST_MANIFEST ??
        path.join(workspaceRoot, '.webstir', 'backend-manifest.json');
    const readyText = options.readyText ?? process.env.WEBSTIR_BACKEND_TEST_READY ?? DEFAULT_READY_TEXT;
    const readyTimeoutMs = options.readyTimeoutMs ?? readInt(process.env.WEBSTIR_BACKEND_TEST_READY_TIMEOUT, DEFAULT_READY_TIMEOUT_MS);
    if (!existsSync(entry)) {
        throw new Error(`Backend test entry not found at ${entry}. Run the backend build before executing backend tests.`);
    }
    const requestedPort = options.port ?? readInt(process.env.WEBSTIR_BACKEND_TEST_PORT, DEFAULT_PORT);
    const port = await findOpenPort(requestedPort);
    const env = createRuntimeEnv({
        workspaceRoot,
        port,
        overrides: options.env
    });
    const manifest = await loadManifest(manifestPath);
    const child = spawn(process.execPath, [entry], {
        cwd: workspaceRoot,
        env,
        stdio: ['ignore', 'pipe', 'pipe']
    });
    try {
        await waitForReady(child, readyText, readyTimeoutMs);
    }
    catch (error) {
        await stopProcess(child);
        throw error;
    }
    const baseUrl = new URL(env.API_BASE_URL ?? `http://127.0.0.1:${port}`);
    const context = {
        baseUrl: baseUrl.toString(),
        url: baseUrl,
        port,
        manifest,
        routes: Array.isArray(manifest?.routes) ? manifest.routes : [],
        env,
        request: async (pathOrUrl = '/', init) => {
            const target = toUrl(baseUrl, pathOrUrl);
            return await fetch(target, init);
        }
    };
    return {
        context,
        async stop() {
            await stopProcess(child);
        }
    };
}
export function backendTest(name, callback) {
    const globalTest = globalThis.test;
    if (typeof globalTest !== 'function') {
        throw new Error('backendTest() requires the @webstir-io/webstir-testing runtime.');
    }
    globalTest(name, async () => {
        const context = getBackendTestContext();
        if (!context) {
            throw new Error('Backend test context not available. Ensure backend tests run via the Webstir CLI (`webstir test`).');
        }
        await callback(context);
    });
}
function toUrl(base, pathOrUrl) {
    if (pathOrUrl instanceof URL) {
        return pathOrUrl.toString();
    }
    if (/^https?:/i.test(pathOrUrl)) {
        return pathOrUrl;
    }
    return new URL(pathOrUrl, base).toString();
}
function readInt(value, fallback) {
    if (!value)
        return fallback;
    const parsed = Number.parseInt(value, 10);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
}
async function findOpenPort(start, attempts = 10) {
    let candidate = start;
    for (let index = 0; index < attempts; index += 1) {
        // eslint-disable-next-line no-await-in-loop
        if (await isPortAvailable(candidate)) {
            return candidate;
        }
        candidate += 1;
    }
    throw new Error(`Unable to find an open port for backend tests (tried starting at ${start}).`);
}
function isPortAvailable(port) {
    return new Promise((resolve) => {
        const server = net.createServer();
        server.once('error', () => {
            server.close(() => resolve(false));
        });
        server.once('listening', () => {
            server.close(() => resolve(true));
        });
        server.listen(port, '127.0.0.1');
    });
}
function createRuntimeEnv(options) {
    const overrides = {};
    for (const [key, value] of Object.entries(options.overrides ?? {})) {
        if (value !== undefined) {
            overrides[key] = value;
        }
    }
    const baseUrl = overrides.API_BASE_URL ?? process.env.API_BASE_URL ?? `http://127.0.0.1:${options.port}`;
    return {
        ...process.env,
        ...overrides,
        PORT: String(options.port),
        API_BASE_URL: baseUrl,
        NODE_ENV: overrides.NODE_ENV ?? process.env.NODE_ENV ?? 'test',
        WORKSPACE_ROOT: options.workspaceRoot,
        WEBSTIR_BACKEND_TEST_RUN: '1'
    };
}
async function loadManifest(manifestPath) {
    try {
        const raw = await readFile(manifestPath, 'utf8');
        return JSON.parse(raw);
    }
    catch {
        return null;
    }
}
function emitModuleEvent(level, message) {
    const payload = JSON.stringify({ type: level, message });
    process.stdout.write(`WEBSTIR_MODULE_EVENT ${payload}\n`);
}
async function waitForReady(child, readyText, timeoutMs) {
    const normalized = readyText
        .split('|')
        .map((token) => token.trim())
        .filter(Boolean);
    const readinessMatches = (line) => (normalized.length === 0 ? line.length > 0 : normalized.some((token) => line.includes(token)));
    await new Promise((resolve, reject) => {
        const cleanup = () => {
            child.stdout?.off('data', onStdout);
            child.stderr?.off('data', onStderr);
            child.off('exit', onExit);
            clearTimeout(timer);
        };
        const onStdout = (chunk) => {
            const text = chunk.toString();
            for (const line of text.split(/\r?\n/)) {
                if (!line)
                    continue;
                emitModuleEvent('info', line);
                if (readinessMatches(line)) {
                    cleanup();
                    resolve();
                }
            }
        };
        const onStderr = (chunk) => {
            const text = chunk.toString();
            for (const line of text.split(/\r?\n/)) {
                if (!line)
                    continue;
                emitModuleEvent('error', line);
                if (readinessMatches(line)) {
                    cleanup();
                    resolve();
                }
            }
        };
        const onExit = (code) => {
            cleanup();
            reject(new Error(`Backend test server exited before it became ready (code ${code ?? 'null'}).`));
        };
        const timer = setTimeout(() => {
            cleanup();
            emitModuleEvent('error', 'Backend test server readiness timed out.');
            reject(new Error(`Backend test server did not become ready within ${timeoutMs}ms. Check server logs for details.`));
        }, timeoutMs);
        child.stdout?.on('data', onStdout);
        child.stderr?.on('data', onStderr);
        child.once('exit', onExit);
    });
}
async function stopProcess(child) {
    if (!child || child.killed || child.exitCode !== null) {
        return;
    }
    child.kill('SIGTERM');
    try {
        await once(child, 'exit');
    }
    catch {
        // ignore
    }
}
