"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.runBuild = runBuild;
exports.runPublish = runPublish;
exports.runRebuild = runRebuild;
exports.runAddPage = runAddPage;
const manifest_js_1 = require("./config/manifest.js");
const workspace_js_1 = require("./utils/workspace.js");
const manifest_js_2 = require("./utils/manifest.js");
const pipeline_js_1 = require("./pipeline.js");
const pageScaffold_js_1 = require("./utils/pageScaffold.js");
async function prepareConfig(workspaceRoot) {
    const config = (0, workspace_js_1.buildConfig)(workspaceRoot);
    await (0, manifest_js_2.ensureToolsDirectory)(workspaceRoot);
    await (0, manifest_js_1.writeConfigManifest)({
        outputPath: (0, manifest_js_2.resolveManifestPath)(workspaceRoot),
        data: config
    });
    return config;
}
async function runBuild(options) {
    const config = await prepareConfig(options.workspaceRoot);
    console.info('[webstir-frontend] Running build pipeline...');
    await (0, pipeline_js_1.runPipeline)(config, 'build', { changedFile: options.changedFile });
    console.info('[webstir-frontend] Build pipeline completed.');
}
async function runPublish(options) {
    const config = await prepareConfig(options.workspaceRoot);
    console.info('[webstir-frontend] Running publish pipeline...');
    await (0, pipeline_js_1.runPipeline)(config, 'publish');
    console.info('[webstir-frontend] Publish pipeline completed.');
}
async function runRebuild(options) {
    const config = await prepareConfig(options.workspaceRoot);
    console.info('[webstir-frontend] Running rebuild pipeline...');
    await (0, pipeline_js_1.runPipeline)(config, 'build', { changedFile: options.changedFile });
    console.info('[webstir-frontend] Rebuild pipeline completed.');
}
async function runAddPage(options) {
    const config = await prepareConfig(options.workspaceRoot);
    console.info('[webstir-frontend] Creating page scaffold...');
    await (0, pageScaffold_js_1.createPageScaffold)({
        workspaceRoot: options.workspaceRoot,
        pageName: options.pageName,
        paths: {
            pages: config.paths.src.pages,
            app: config.paths.src.app
        }
    });
    console.info('[webstir-frontend] Page scaffold created.');
}
