"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.updatePageManifest = updatePageManifest;
exports.readPageManifest = readPageManifest;
const node_path_1 = __importDefault(require("node:path"));
const fs_js_1 = require("../utils/fs.js");
const MANIFEST_FILENAME = 'manifest.json';
async function updatePageManifest(directory, pageName, updater) {
    const manifestPath = node_path_1.default.join(directory, MANIFEST_FILENAME);
    await (0, fs_js_1.ensureDir)(directory);
    const manifest = (await (0, fs_js_1.readJson)(manifestPath)) ?? { pages: {} };
    const pageManifest = manifest.pages[pageName] ?? {};
    updater(pageManifest);
    manifest.pages[pageName] = pageManifest;
    await (0, fs_js_1.writeJson)(manifestPath, manifest);
}
async function readPageManifest(directory, pageName) {
    const manifestPath = node_path_1.default.join(directory, MANIFEST_FILENAME);
    const manifest = (await (0, fs_js_1.readJson)(manifestPath)) ?? { pages: {} };
    return manifest.pages[pageName] ?? {};
}
