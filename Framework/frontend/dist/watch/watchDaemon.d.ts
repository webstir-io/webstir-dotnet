import type { WatchDaemonOptions } from './types.js';
export declare class WatchDaemon {
    private readonly coordinator;
    private readonly options;
    private readonly shutdownPromise;
    private resolveShutdown;
    private commandQueue;
    private isShuttingDown;
    private rl?;
    constructor(options: WatchDaemonOptions);
    run(): Promise<void>;
    private setupCommandLoop;
    private setupSignalHandlers;
    private processLine;
    private handleCommand;
    private shutdown;
}
