"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.runBuild = runBuild;
exports.runPublish = runPublish;
exports.runRebuild = runRebuild;
const manifest_js_1 = require("./config/manifest.js");
const workspace_js_1 = require("./utils/workspace.js");
const manifest_js_2 = require("./utils/manifest.js");
const pipeline_js_1 = require("./pipeline.js");
async function runBuild(options) {
    const config = (0, workspace_js_1.buildConfig)(options.workspaceRoot);
    await (0, manifest_js_2.ensureToolsDirectory)(options.workspaceRoot);
    await (0, manifest_js_1.writeConfigManifest)({
        outputPath: (0, manifest_js_2.resolveManifestPath)(options.workspaceRoot),
        data: config
    });
    console.info('[webstir-frontend] Running build pipeline...');
    await (0, pipeline_js_1.runPipeline)(config, 'build', { changedFile: options.changedFile });
    console.info('[webstir-frontend] Build pipeline completed.');
}
async function runPublish(options) {
    const config = (0, workspace_js_1.buildConfig)(options.workspaceRoot);
    await (0, manifest_js_2.ensureToolsDirectory)(options.workspaceRoot);
    await (0, manifest_js_1.writeConfigManifest)({
        outputPath: (0, manifest_js_2.resolveManifestPath)(options.workspaceRoot),
        data: config
    });
    console.info('[webstir-frontend] Running publish pipeline...');
    await (0, pipeline_js_1.runPipeline)(config, 'publish');
    console.info('[webstir-frontend] Publish pipeline completed.');
}
async function runRebuild(options) {
    const config = (0, workspace_js_1.buildConfig)(options.workspaceRoot);
    await (0, manifest_js_2.ensureToolsDirectory)(options.workspaceRoot);
    await (0, manifest_js_1.writeConfigManifest)({
        outputPath: (0, manifest_js_2.resolveManifestPath)(options.workspaceRoot),
        data: config
    });
    console.info('[webstir-frontend] Running rebuild pipeline...');
    await (0, pipeline_js_1.runPipeline)(config, 'build', { changedFile: options.changedFile });
    console.info('[webstir-frontend] Rebuild pipeline completed.');
}
