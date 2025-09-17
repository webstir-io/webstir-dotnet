"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.runPipeline = runPipeline;
const node_perf_hooks_1 = require("node:perf_hooks");
const index_js_1 = require("./builders/index.js");
async function runPipeline(config, mode, options = {}) {
    const context = { config, changedFile: options.changedFile };
    const builders = (0, index_js_1.createBuilders)(context);
    if (builders.length === 0) {
        return;
    }
    for (const builder of builders) {
        const start = node_perf_hooks_1.performance.now();
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
            const end = node_perf_hooks_1.performance.now();
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
