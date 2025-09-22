import { performance } from 'node:perf_hooks';
import { createBuilders } from './builders/index.js';
export async function runPipeline(config, mode, options = {}) {
    const context = { config, changedFile: options.changedFile };
    const builders = createBuilders(context);
    if (builders.length === 0) {
        return;
    }
    for (const builder of builders) {
        const start = performance.now();
        try {
            if (mode === 'build') {
                await builder.build(context);
            }
            else {
                await builder.publish(context);
            }
        }
        catch (error) {
            throw wrapPipelineError(builder.name, mode, error);
        }
        finally {
            const end = performance.now();
            const duration = end - start;
            console.info(`[webstir-frontend] ${mode}:${builder.name} completed in ${duration.toFixed(1)}ms`);
        }
    }
}
function wrapPipelineError(name, mode, error) {
    if (error instanceof Error) {
        error.message = `[${mode}:${name}] ${error.message}`;
        return error;
    }
    return new Error(`[${mode}:${name}] ${String(error)}`);
}
