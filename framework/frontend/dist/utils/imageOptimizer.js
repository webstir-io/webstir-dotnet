"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.optimizeImages = optimizeImages;
exports.getImageDimensions = getImageDimensions;
const node_path_1 = __importDefault(require("node:path"));
const sharp_1 = __importDefault(require("sharp"));
const glob_1 = require("glob");
const fs_js_1 = require("./fs.js");
const constants_js_1 = require("./constants.js");
const TRANSCODABLE_EXTENSIONS = new Set([
    constants_js_1.EXTENSIONS.png,
    constants_js_1.EXTENSIONS.jpg,
    constants_js_1.EXTENSIONS.jpeg
]);
async function optimizeImages(sourceDir, destinationDir) {
    if (!(await (0, fs_js_1.pathExists)(sourceDir))) {
        await (0, fs_js_1.emptyDir)(destinationDir);
        return;
    }
    await (0, fs_js_1.emptyDir)(destinationDir);
    const files = await (0, glob_1.glob)('**/*', { cwd: sourceDir, nodir: true });
    await Promise.all(files.map(async (relative) => {
        const sourcePath = node_path_1.default.join(sourceDir, relative);
        const destinationPath = node_path_1.default.join(destinationDir, relative);
        await (0, fs_js_1.ensureDir)(node_path_1.default.dirname(destinationPath));
        await (0, fs_js_1.copy)(sourcePath, destinationPath);
        const extension = node_path_1.default.extname(sourcePath).toLowerCase();
        if (!TRANSCODABLE_EXTENSIONS.has(extension)) {
            return;
        }
        await Promise.all([
            createWebpVariant(sourcePath, replaceExtension(destinationPath, constants_js_1.EXTENSIONS.webp)),
            createAvifVariant(sourcePath, replaceExtension(destinationPath, constants_js_1.EXTENSIONS.avif))
        ]);
    }));
}
async function getImageDimensions(filePath) {
    try {
        const metadata = await (0, sharp_1.default)(filePath).metadata();
        if (typeof metadata.width === 'number' && typeof metadata.height === 'number') {
            return { width: metadata.width, height: metadata.height };
        }
    }
    catch {
        // Ignore errors – the caller can continue without dimensions.
    }
    return null;
}
function replaceExtension(filePath, extension) {
    const parsed = node_path_1.default.parse(filePath);
    return node_path_1.default.join(parsed.dir, `${parsed.name}${extension}`);
}
async function createWebpVariant(sourcePath, destinationPath) {
    try {
        await (0, sharp_1.default)(sourcePath)
            .webp({ quality: 75 })
            .toFile(destinationPath);
    }
    catch {
        // Ignore failures; fall back to original image only.
    }
}
async function createAvifVariant(sourcePath, destinationPath) {
    try {
        await (0, sharp_1.default)(sourcePath)
            .avif({ quality: 45 })
            .toFile(destinationPath);
    }
    catch {
        // Ignore failures; fall back to original image only.
    }
}
