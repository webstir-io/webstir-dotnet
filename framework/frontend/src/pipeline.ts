import { performance } from 'node:perf_hooks';
import type { FrontendConfig } from './types.js';
import { createBuilders } from './builders/index.js';
import type { Builder, BuilderContext } from './builders/types.js';

export interface PipelineOptions {
    readonly changedFile?: string;
}

export type PipelineMode = 'build' | 'publish';

export async function runPipeline(config: FrontendConfig, mode: PipelineMode, options: PipelineOptions = {}): Promise<void> {
    const context: BuilderContext = { config, changedFile: options.changedFile };
    const builders: Builder[] = createBuilders(context);

    if (builders.length === 0) {
        return;
    }

    for (const builder of builders) {
        const start = performance.now();
        try {
            if (mode === 'build') {
                await builder.build(context);
            } else {
                await builder.publish(context);
            }
        } catch (error) {
            throw wrapPipelineError(builder.name, mode, error);
        } finally {
            const end = performance.now();
            const duration = end - start;
            console.info(`[webstir-frontend] ${mode}:${builder.name} completed in ${duration.toFixed(1)}ms`);
        }
    }
}

function wrapPipelineError(name: string, mode: PipelineMode, error: unknown): Error {
    if (error instanceof Error) {
        error.message = `[${mode}:${name}] ${error.message}`;
        return error;
    }

    return new Error(`[${mode}:${name}] ${String(error)}`);
}
