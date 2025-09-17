import type { FrontendConfig } from './types.js';
export interface PipelineOptions {
    readonly changedFile?: string;
}
export type PipelineMode = 'build' | 'publish';
export declare function runPipeline(config: FrontendConfig, mode: PipelineMode, options?: PipelineOptions): Promise<void>;
