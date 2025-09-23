import type { WatchChangeIntent, WatchCoordinatorOptions } from './types.js';
export declare class WatchCoordinator {
    private readonly workspaceRoot;
    private readonly jsContexts;
    private config?;
    private isStopping;
    private queue;
    constructor(options: WatchCoordinatorOptions);
    start(): Promise<void>;
    reload(): Promise<void>;
    handleChange(intent: WatchChangeIntent): Promise<void>;
    stop(): Promise<void>;
    private enqueue;
    private refreshJavaScriptContexts;
    private ensureJavaScriptContext;
    private runFullBuildCycle;
    private runAdditionalBuilders;
    private runBuilderWithDiagnostics;
    private emitPipelineSuccess;
    private getRelativeChange;
    private runJavaScriptBuild;
    private executeJavaScriptBuild;
    private resolveTargetPages;
    private serializeSummary;
    private emitJavaScriptFailure;
    private resolveChangedFile;
    private requireConfig;
    private logUnexpectedError;
}
