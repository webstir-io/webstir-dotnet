"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.createStaticAssetsBuilder = createStaticAssetsBuilder;
const node_path_1 = __importDefault(require("node:path"));
const constants_js_1 = require("../core/constants.js");
const fs_js_1 = require("../utils/fs.js");
const changedFile_js_1 = require("../utils/changedFile.js");
const imageOptimizer_js_1 = require("../assets/imageOptimizer.js");
const pathMatch_js_1 = require("../utils/pathMatch.js");
const IMAGE_EXTENSIONS = [
    constants_js_1.EXTENSIONS.png,
    constants_js_1.EXTENSIONS.jpg,
    constants_js_1.EXTENSIONS.jpeg,
    constants_js_1.EXTENSIONS.gif,
    constants_js_1.EXTENSIONS.svg,
    constants_js_1.EXTENSIONS.webp,
    constants_js_1.EXTENSIONS.ico
];
const FONT_EXTENSIONS = [
    constants_js_1.EXTENSIONS.woff,
    constants_js_1.EXTENSIONS.woff2,
    constants_js_1.EXTENSIONS.ttf,
    constants_js_1.EXTENSIONS.otf,
    constants_js_1.EXTENSIONS.eot
];
const MEDIA_EXTENSIONS = [
    constants_js_1.EXTENSIONS.mp3,
    constants_js_1.EXTENSIONS.m4a,
    constants_js_1.EXTENSIONS.wav,
    constants_js_1.EXTENSIONS.ogg,
    constants_js_1.EXTENSIONS.mp4,
    constants_js_1.EXTENSIONS.webm,
    constants_js_1.EXTENSIONS.mov
];
function createStaticAssetsBuilder(context) {
    return {
        name: 'static-assets',
        async build() {
            await copyStaticAssets(context, false);
        },
        async publish() {
            await copyStaticAssets(context, true);
        }
    };
}
async function copyStaticAssets(context, isProduction) {
    const { config } = context;
    if (!(0, changedFile_js_1.shouldProcess)(context, [
        { directory: config.paths.src.images, extensions: IMAGE_EXTENSIONS },
        { directory: config.paths.src.fonts, extensions: FONT_EXTENSIONS },
        { directory: config.paths.src.media, extensions: MEDIA_EXTENSIONS }
    ])) {
        return;
    }
    const targets = [
        { source: config.paths.src.images, build: config.paths.build.frontend, dist: config.paths.dist.frontend, folder: constants_js_1.FOLDERS.images, extensions: IMAGE_EXTENSIONS },
        { source: config.paths.src.fonts, build: config.paths.build.frontend, dist: config.paths.dist.frontend, folder: constants_js_1.FOLDERS.fonts, extensions: FONT_EXTENSIONS },
        { source: config.paths.src.media, build: config.paths.build.frontend, dist: config.paths.dist.frontend, folder: constants_js_1.FOLDERS.media, extensions: MEDIA_EXTENSIONS }
    ];
    for (const target of targets) {
        if (!(await (0, fs_js_1.pathExists)(target.source))) {
            continue;
        }
        const changedRelative = (0, pathMatch_js_1.relativePathWithin)(context.changedFile, target.source);
        const buildDestination = node_path_1.default.join(target.build, target.folder);
        if (!context.changedFile || !changedRelative) {
            await (0, fs_js_1.emptyDir)(buildDestination);
            await (0, fs_js_1.copy)(target.source, buildDestination);
            if (isProduction) {
                const distDestination = node_path_1.default.join(target.dist, target.folder);
                if (target.folder === constants_js_1.FOLDERS.images) {
                    if (config.features.imageOptimization) {
                        await (0, imageOptimizer_js_1.optimizeImages)(buildDestination, distDestination);
                    }
                    else {
                        await (0, fs_js_1.emptyDir)(distDestination);
                        await (0, fs_js_1.copy)(buildDestination, distDestination);
                    }
                }
                else {
                    await (0, fs_js_1.emptyDir)(distDestination);
                    await (0, fs_js_1.copy)(buildDestination, distDestination);
                }
            }
            continue;
        }
        await copySingleAsset(target.source, buildDestination, changedRelative);
        if (isProduction) {
            const distDestination = node_path_1.default.join(target.dist, target.folder);
            if (target.folder === constants_js_1.FOLDERS.images) {
                if (config.features.imageOptimization) {
                    await (0, imageOptimizer_js_1.optimizeImages)(buildDestination, distDestination, [changedRelative]);
                }
                else {
                    await syncImageWithoutOptimization(buildDestination, distDestination, changedRelative);
                }
            }
            else {
                const sourcePath = node_path_1.default.join(target.source, changedRelative);
                const destPath = node_path_1.default.join(distDestination, changedRelative);
                if (await (0, fs_js_1.pathExists)(sourcePath)) {
                    await (0, fs_js_1.ensureDir)(node_path_1.default.dirname(destPath));
                    await (0, fs_js_1.copy)(sourcePath, destPath);
                }
                else {
                    await (0, fs_js_1.remove)(destPath).catch(() => undefined);
                }
            }
        }
    }
}
async function copySingleAsset(sourceRoot, buildRoot, relativePath) {
    const sourcePath = node_path_1.default.join(sourceRoot, relativePath);
    const destinationPath = node_path_1.default.join(buildRoot, relativePath);
    if (await (0, fs_js_1.pathExists)(sourcePath)) {
        await (0, fs_js_1.ensureDir)(node_path_1.default.dirname(destinationPath));
        await (0, fs_js_1.copy)(sourcePath, destinationPath);
    }
    else {
        await (0, fs_js_1.remove)(destinationPath).catch(() => undefined);
    }
}
async function syncImageWithoutOptimization(buildRoot, distRoot, relativePath) {
    const sourcePath = node_path_1.default.join(buildRoot, relativePath);
    const destinationPath = node_path_1.default.join(distRoot, relativePath);
    if (await (0, fs_js_1.pathExists)(sourcePath)) {
        await (0, fs_js_1.ensureDir)(node_path_1.default.dirname(destinationPath));
        await (0, fs_js_1.copy)(sourcePath, destinationPath);
    }
    else {
        await (0, fs_js_1.remove)(destinationPath).catch(() => undefined);
    }
    await Promise.all([
        (0, fs_js_1.remove)(`${destinationPath}${constants_js_1.EXTENSIONS.webp}`).catch(() => undefined),
        (0, fs_js_1.remove)(`${destinationPath}${constants_js_1.EXTENSIONS.avif}`).catch(() => undefined)
    ]);
}
