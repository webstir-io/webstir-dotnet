"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.createJavaScriptBuilder = createJavaScriptBuilder;
const node_path_1 = __importDefault(require("node:path"));
const esbuild_1 = require("esbuild");
const constants_js_1 = require("../core/constants.js");
const pages_js_1 = require("../core/pages.js");
const fs_js_1 = require("../utils/fs.js");
const assetManifest_js_1 = require("../assets/assetManifest.js");
const precompression_js_1 = require("../assets/precompression.js");
const changedFile_js_1 = require("../utils/changedFile.js");
const pathMatch_js_1 = require("../utils/pathMatch.js");
const ENTRY_EXTENSIONS = ['.ts', '.tsx', '.js'];
function createJavaScriptBuilder(context) {
    return {
        name: 'javascript',
        async build() {
            await bundleJavaScript(context, false);
        },
        async publish() {
            await bundleJavaScript(context, true);
        }
    };
}
async function bundleJavaScript(context, isProduction) {
    const { config } = context;
    if (!(0, changedFile_js_1.shouldProcess)(context, [
        {
            directory: config.paths.src.frontend,
            extensions: [constants_js_1.EXTENSIONS.ts, constants_js_1.EXTENSIONS.js, '.tsx', '.jsx']
        }
    ])) {
        return;
    }
    const targetPage = (0, pathMatch_js_1.findPageFromChangedFile)(context.changedFile, config.paths.src.pages);
    const pages = await (0, pages_js_1.getPages)(config.paths.src.pages);
    for (const page of pages) {
        if (targetPage && page.name !== targetPage) {
            continue;
        }
        const entryPoint = await resolveEntryPoint(page.directory);
        if (!entryPoint) {
            continue;
        }
        if (isProduction) {
            await buildForProduction(config, page.name, entryPoint);
        }
        else {
            await buildForDevelopment(config, page.name, entryPoint);
        }
    }
    await copyRefreshScript(config);
}
async function buildForDevelopment(config, pageName, entryPoint) {
    const outputDir = node_path_1.default.join(config.paths.build.frontend, constants_js_1.FOLDERS.pages, pageName);
    await (0, fs_js_1.ensureDir)(outputDir);
    const outfile = node_path_1.default.join(outputDir, `${constants_js_1.FILES.index}${constants_js_1.EXTENSIONS.js}`);
    await (0, esbuild_1.build)({
        entryPoints: [entryPoint],
        bundle: true,
        format: 'esm',
        target: 'es2020',
        platform: 'browser',
        sourcemap: true,
        outfile,
        logLevel: 'silent'
    });
}
async function buildForProduction(config, pageName, entryPoint) {
    const outputDir = node_path_1.default.join(config.paths.dist.frontend, constants_js_1.FOLDERS.pages, pageName);
    await (0, fs_js_1.ensureDir)(outputDir);
    const result = await (0, esbuild_1.build)({
        entryPoints: [entryPoint],
        bundle: true,
        format: 'esm',
        target: 'es2020',
        platform: 'browser',
        minify: true,
        sourcemap: false,
        outdir: outputDir,
        entryNames: `${constants_js_1.FILES.index}-[hash]`,
        assetNames: 'assets/[name]-[hash]',
        metafile: true,
        logLevel: 'silent'
    });
    const outputs = result.metafile?.outputs ?? {};
    const scriptPath = Object.keys(outputs).find((file) => file.endsWith('.js'));
    if (!scriptPath) {
        throw new Error(`esbuild did not produce a JavaScript bundle for page '${pageName}'.`);
    }
    const fileName = node_path_1.default.basename(scriptPath);
    const absolutePath = node_path_1.default.join(outputDir, fileName);
    await (0, precompression_js_1.createCompressedVariants)(absolutePath);
    await (0, assetManifest_js_1.updatePageManifest)(outputDir, pageName, (manifest) => {
        manifest.js = fileName;
    });
}
async function copyRefreshScript(config) {
    const refreshScript = node_path_1.default.join(config.paths.src.app, constants_js_1.FILES.refreshJs);
    if (!(await (0, fs_js_1.pathExists)(refreshScript))) {
        return;
    }
    const destination = node_path_1.default.join(config.paths.build.frontend, constants_js_1.FILES.refreshJs);
    await (0, fs_js_1.ensureDir)(node_path_1.default.dirname(destination));
    await (0, fs_js_1.copy)(refreshScript, destination);
}
async function resolveEntryPoint(pageDirectory) {
    for (const extension of ENTRY_EXTENSIONS) {
        const candidate = node_path_1.default.join(pageDirectory, `${constants_js_1.FILES.index}${extension}`);
        if (await (0, fs_js_1.pathExists)(candidate)) {
            return candidate;
        }
    }
    return null;
}
