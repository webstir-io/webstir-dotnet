"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.isInsideDirectory = isInsideDirectory;
exports.findPageFromChangedFile = findPageFromChangedFile;
const node_path_1 = __importDefault(require("node:path"));
function isInsideDirectory(filePath, directory) {
    const resolvedFile = node_path_1.default.resolve(filePath);
    const resolvedDirectory = node_path_1.default.resolve(directory);
    const relative = node_path_1.default.relative(resolvedDirectory, resolvedFile);
    return relative === '' || (!relative.startsWith('..') && !node_path_1.default.isAbsolute(relative));
}
function findPageFromChangedFile(changedFile, pagesRoot) {
    if (!changedFile) {
        return null;
    }
    const resolvedChanged = node_path_1.default.resolve(changedFile);
    const resolvedPagesRoot = node_path_1.default.resolve(pagesRoot);
    if (!isInsideDirectory(resolvedChanged, resolvedPagesRoot)) {
        return null;
    }
    const relative = node_path_1.default.relative(resolvedPagesRoot, resolvedChanged);
    const segments = relative.split(node_path_1.default.sep);
    return segments.length > 0 && segments[0] ? segments[0] : null;
}
