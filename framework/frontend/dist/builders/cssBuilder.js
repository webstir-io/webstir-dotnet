"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.createCssBuilder = createCssBuilder;
const node_path_1 = __importDefault(require("node:path"));
const postcss_1 = __importDefault(require("postcss"));
const autoprefixer_1 = __importDefault(require("autoprefixer"));
const csso_1 = __importDefault(require("csso"));
const constants_js_1 = require("../utils/constants.js");
const fs_js_1 = require("../utils/fs.js");
const pages_js_1 = require("../utils/pages.js");
const hash_js_1 = require("../utils/hash.js");
const assetManifest_js_1 = require("../utils/assetManifest.js");
const precompression_js_1 = require("../utils/precompression.js");
const changedFile_js_1 = require("../utils/changedFile.js");
const pathMatch_js_1 = require("../utils/pathMatch.js");
const MODULE_SUFFIX = '.module';
function createCssBuilder(context) {
    return {
        name: 'css',
        async build() {
            await processCss(context, false);
        },
        async publish() {
            await processCss(context, true);
        }
    };
}
async function processCss(context, isProduction) {
    const { config } = context;
    if (!(0, changedFile_js_1.shouldProcess)(context, [
        { directory: config.paths.src.pages, extensions: [constants_js_1.EXTENSIONS.css] },
        { directory: config.paths.src.frontend, extensions: [constants_js_1.EXTENSIONS.css] }
    ])) {
        return;
    }
    const targetPage = (0, pathMatch_js_1.findPageFromChangedFile)(context.changedFile, config.paths.src.pages);
    const pages = await (0, pages_js_1.getPages)(config.paths.src.pages);
    for (const page of pages) {
        if (targetPage && page.name !== targetPage) {
            continue;
        }
        const entryPath = await resolveCssEntry(page.directory);
        if (!entryPath) {
            continue;
        }
        const css = await (0, fs_js_1.readFile)(entryPath);
        const processor = (0, postcss_1.default)([autoprefixer_1.default]);
        const processed = await processor.process(css, { from: entryPath, map: !isProduction ? { inline: true } : false });
        if (isProduction) {
            await emitProductionCss(config, page.name, processed.css);
        }
        else {
            await emitDevelopmentCss(config, page.name, processed.css);
        }
    }
}
async function emitDevelopmentCss(config, pageName, css) {
    const outputDir = node_path_1.default.join(config.paths.build.frontend, constants_js_1.FOLDERS.pages, pageName);
    await (0, fs_js_1.ensureDir)(outputDir);
    const outputPath = node_path_1.default.join(outputDir, `${constants_js_1.FILES.index}${constants_js_1.EXTENSIONS.css}`);
    await (0, fs_js_1.writeFile)(outputPath, css);
}
async function emitProductionCss(config, pageName, css) {
    const minified = csso_1.default.minify(css).css;
    const hash = (0, hash_js_1.hashContent)(minified);
    const fileName = `${constants_js_1.FILES.index}-${hash}${constants_js_1.EXTENSIONS.css}`;
    const outputDir = node_path_1.default.join(config.paths.dist.frontend, constants_js_1.FOLDERS.pages, pageName);
    await (0, fs_js_1.ensureDir)(outputDir);
    const outputPath = node_path_1.default.join(outputDir, fileName);
    await (0, fs_js_1.writeFile)(outputPath, minified);
    await (0, precompression_js_1.createCompressedVariants)(outputPath);
    await (0, assetManifest_js_1.updatePageManifest)(outputDir, pageName, (manifest) => {
        manifest.css = fileName;
    });
}
async function resolveCssEntry(pageDirectory) {
    const modulePath = node_path_1.default.join(pageDirectory, `${constants_js_1.FILES.index}${MODULE_SUFFIX}${constants_js_1.EXTENSIONS.css}`);
    if (await (0, fs_js_1.pathExists)(modulePath)) {
        return modulePath;
    }
    const plainPath = node_path_1.default.join(pageDirectory, `${constants_js_1.FILES.index}${constants_js_1.EXTENSIONS.css}`);
    if (await (0, fs_js_1.pathExists)(plainPath)) {
        return plainPath;
    }
    return null;
}
