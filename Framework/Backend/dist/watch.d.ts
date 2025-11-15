export interface WatchHandle {
    stop(): Promise<void>;
}
export interface StartWatchOptions {
    readonly workspaceRoot: string;
    readonly env?: Record<string, string | undefined>;
}
export declare function startBackendWatch(options: StartWatchOptions): Promise<WatchHandle>;
