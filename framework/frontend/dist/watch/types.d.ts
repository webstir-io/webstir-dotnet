export type WatchDaemonCommand = {
    readonly type: 'start';
} | {
    readonly type: 'change';
    readonly path: string;
} | {
    readonly type: 'reload';
} | {
    readonly type: 'shutdown';
} | {
    readonly type: 'ping';
    readonly id?: string;
};
export interface WatchDaemonOptions {
    readonly workspaceRoot: string;
    readonly autoStart?: boolean;
    readonly verbose?: boolean;
    readonly hmrVerbose?: boolean;
}
export interface WatchCoordinatorOptions {
    readonly workspaceRoot: string;
    readonly verbose?: boolean;
    readonly hmrVerbose?: boolean;
}
export interface WatchChangeIntent {
    readonly path?: string;
}
