import process from 'node:process';
import { createInterface } from 'node:readline';
import { emitDiagnostic } from '../core/diagnostics.js';
import { WatchCoordinator } from './watchCoordinator.js';
export class WatchDaemon {
    coordinator;
    options;
    shutdownPromise;
    resolveShutdown = null;
    commandQueue = Promise.resolve();
    isShuttingDown = false;
    rl;
    constructor(options) {
        this.options = options;
        this.coordinator = new WatchCoordinator({
            workspaceRoot: options.workspaceRoot,
            verbose: options.verbose ?? false,
            hmrVerbose: options.hmrVerbose ?? false
        });
        this.shutdownPromise = new Promise((resolve) => {
            this.resolveShutdown = resolve;
        });
    }
    async run() {
        if (this.options.autoStart !== false) {
            await this.coordinator.start();
        }
        this.setupSignalHandlers();
        this.setupCommandLoop();
        await this.shutdownPromise;
    }
    setupCommandLoop() {
        if (process.stdin.isTTY) {
            process.stdin.setRawMode(false);
        }
        process.stdin.setEncoding('utf8');
        this.rl = createInterface({ input: process.stdin, crlfDelay: Infinity });
        this.rl.on('line', (line) => this.processLine(line));
        this.rl.on('close', () => {
            void this.shutdown();
        });
    }
    setupSignalHandlers() {
        const shutdown = () => {
            void this.shutdown();
        };
        process.on('SIGINT', shutdown);
        process.on('SIGTERM', shutdown);
    }
    processLine(rawLine) {
        const line = rawLine.trim();
        if (line.length === 0) {
            return;
        }
        let command = null;
        try {
            command = JSON.parse(line);
        }
        catch (error) {
            emitDiagnostic({
                code: 'frontend.watch.command.invalid',
                kind: 'watch-daemon',
                stage: 'command',
                severity: 'warning',
                message: `Discarding invalid command payload: ${String(error)}`
            });
            return;
        }
        this.commandQueue = this.commandQueue.then(() => this.handleCommand(command)).catch((error) => {
            emitDiagnostic({
                code: 'frontend.watch.command.failure',
                kind: 'watch-daemon',
                stage: 'command',
                severity: 'error',
                message: `Command handling failed: ${error instanceof Error ? error.message : String(error)}`
            });
        });
    }
    async handleCommand(command) {
        switch (command.type) {
            case 'start':
                await this.coordinator.start();
                return;
            case 'reload':
                await this.coordinator.reload();
                return;
            case 'change':
                await this.coordinator.handleChange({ path: command.path });
                return;
            case 'shutdown':
                await this.shutdown();
                return;
            case 'ping':
                emitDiagnostic({
                    code: 'frontend.watch.pong',
                    kind: 'watch-daemon',
                    stage: 'command',
                    severity: 'info',
                    message: 'Watch daemon heartbeat acknowledged.',
                    data: command.id ? { id: command.id } : undefined
                });
                return;
            default:
                emitDiagnostic({
                    code: 'frontend.watch.command.unknown',
                    kind: 'watch-daemon',
                    stage: 'command',
                    severity: 'warning',
                    message: `Unknown watch daemon command: ${command.type}`
                });
                return;
        }
    }
    async shutdown() {
        if (this.isShuttingDown) {
            return;
        }
        this.isShuttingDown = true;
        if (this.rl) {
            this.rl.close();
            this.rl = undefined;
        }
        await this.coordinator.stop();
        this.resolveShutdown?.();
        this.resolveShutdown = null;
    }
}
