import type { FrontendConfig } from './types.js';
import type { PipelineMode } from './pipeline.js';
export interface HookContext {
    readonly config: FrontendConfig;
    readonly mode: PipelineMode;
    readonly workspaceRoot: string;
    readonly builderName?: string;
    readonly changedFile?: string;
}
export type HookHandler = (context: HookContext) => unknown | Promise<unknown>;
export interface ResolvedHooks {
    readonly pipelineBefore: HookHandler[];
    readonly pipelineAfter: HookHandler[];
    readonly builderBefore: Map<string, HookHandler[]>;
    readonly builderAfter: Map<string, HookHandler[]>;
}
export declare function loadHooks(workspaceRoot: string, cacheBust: boolean): Promise<ResolvedHooks>;
export declare function createHookContext(config: FrontendConfig, mode: PipelineMode, changedFile: string | undefined, builderName?: string): HookContext;
export declare function executeHooks(label: string, handlers: HookHandler[], context: HookContext): Promise<void>;
