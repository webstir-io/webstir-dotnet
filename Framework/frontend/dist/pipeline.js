import { performance } from 'node:perf_hooks';
import { createBuilders } from './builders/index.js';
import { createHookContext, executeHooks, loadHooks } from './hooks.js';
export async function runPipeline(config, mode, options = {}) {
    const context = { config, changedFile: options.changedFile };
    const builders = createBuilders(context);
    const hooks = await loadHooks(config.paths.workspace, mode === 'build');
    const pipelineContext = createHookContext(config, mode, options.changedFile);
    await executeHooks('pipeline.beforeAll', hooks.pipelineBefore, pipelineContext);
    let pipelineError;
    try {
        for (const builder of builders) {
            const builderContext = createHookContext(config, mode, options.changedFile, builder.name);
            const beforeHooks = hooks.builderBefore.get(builder.name) ?? [];
            const afterHooks = hooks.builderAfter.get(builder.name) ?? [];
            await executeHooks(`builder.${builder.name}.before`, beforeHooks, builderContext);
            const start = performance.now();
            let builderError;
            let afterHookError;
            try {
                if (mode === 'build') {
                    await builder.build(context);
                }
                else {
                    await builder.publish(context);
                }
            }
            catch (error) {
                builderError = wrapPipelineError(builder.name, mode, error);
            }
            try {
                await executeHooks(`builder.${builder.name}.after`, afterHooks, builderContext);
            }
            catch (error) {
                afterHookError = error;
            }
            const end = performance.now();
            const duration = end - start;
            console.info(`[webstir-frontend] ${mode}:${builder.name} completed in ${duration.toFixed(1)}ms`);
            if (builderError) {
                throw builderError;
            }
            if (afterHookError) {
                throw afterHookError;
            }
        }
    }
    catch (error) {
        pipelineError = error;
    }
    finally {
        try {
            await executeHooks('pipeline.afterAll', hooks.pipelineAfter, pipelineContext);
        }
        catch (hookError) {
            if (!pipelineError) {
                pipelineError = hookError;
            }
        }
    }
    if (pipelineError) {
        throw pipelineError;
    }
}
function wrapPipelineError(name, mode, error) {
    if (error instanceof Error) {
        error.message = `[${mode}:${name}] ${error.message}`;
        return error;
    }
    return new Error(`[${mode}:${name}] ${String(error)}`);
}
