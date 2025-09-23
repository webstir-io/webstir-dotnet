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
}
export interface WatchCoordinatorOptions {
    readonly workspaceRoot: string;
}
export interface WatchChangeIntent {
    readonly path?: string;
}
