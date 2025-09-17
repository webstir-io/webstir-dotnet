"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.FRONTEND_MANIFEST_FILENAME = void 0;
exports.resolveManifestPath = resolveManifestPath;
exports.ensureToolsDirectory = ensureToolsDirectory;
const path_1 = __importDefault(require("path"));
const fs_1 = require("fs");
const constants_js_1 = require("../core/constants.js");
exports.FRONTEND_MANIFEST_FILENAME = 'frontend-manifest.json';
function resolveManifestPath(workspaceRoot) {
    return path_1.default.join(workspaceRoot, constants_js_1.FOLDERS.tools, exports.FRONTEND_MANIFEST_FILENAME);
}
async function ensureToolsDirectory(workspaceRoot) {
    const toolsPath = path_1.default.join(workspaceRoot, constants_js_1.FOLDERS.tools);
    await fs_1.promises.mkdir(toolsPath, { recursive: true });
}
