"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.createStaticAssetsBuilder = createStaticAssetsBuilder;
const node_path_1 = __importDefault(require("node:path"));
const constants_js_1 = require("../utils/constants.js");
const fs_js_1 = require("../utils/fs.js");
const changedFile_js_1 = require("../utils/changedFile.js");
const imageOptimizer_js_1 = require("../utils/imageOptimizer.js");
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
        { source: config.paths.src.images, build: config.paths.build.frontend, dist: config.paths.dist.frontend, folder: constants_js_1.FOLDERS.images },
        { source: config.paths.src.fonts, build: config.paths.build.frontend, dist: config.paths.dist.frontend, folder: constants_js_1.FOLDERS.fonts },
        { source: config.paths.src.media, build: config.paths.build.frontend, dist: config.paths.dist.frontend, folder: constants_js_1.FOLDERS.media }
    ];
    for (const target of targets) {
        if (!(await (0, fs_js_1.pathExists)(target.source))) {
            continue;
        }
        const buildDestination = node_path_1.default.join(target.build, target.folder);
        await (0, fs_js_1.emptyDir)(buildDestination);
        await (0, fs_js_1.copy)(target.source, buildDestination);
        if (isProduction) {
            const distDestination = node_path_1.default.join(target.dist, target.folder);
            if (target.folder === constants_js_1.FOLDERS.images) {
                await (0, imageOptimizer_js_1.optimizeImages)(buildDestination, distDestination);
            }
            else {
                await (0, fs_js_1.emptyDir)(distDestination);
                await (0, fs_js_1.copy)(buildDestination, distDestination);
            }
        }
    }
}
