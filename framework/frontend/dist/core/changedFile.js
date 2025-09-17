"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.shouldProcess = shouldProcess;
exports.isPathInside = isPathInside;
const node_path_1 = __importDefault(require("node:path"));
function shouldProcess(context, rules) {
    const changed = context.changedFile;
    if (!changed) {
        return true;
    }
    const normalizedChanged = node_path_1.default.resolve(changed);
    for (const rule of rules) {
        const normalizedDir = node_path_1.default.resolve(rule.directory);
        if (!isPathInside(normalizedChanged, normalizedDir)) {
            continue;
        }
        if (!rule.extensions || rule.extensions.length === 0) {
            return true;
        }
        const extension = node_path_1.default.extname(normalizedChanged).toLowerCase();
        if (rule.extensions.includes(extension)) {
            return true;
        }
    }
    return false;
}
function isPathInside(target, directory) {
    const relative = node_path_1.default.relative(directory, target);
    return relative === '' || (!relative.startsWith('..') && !node_path_1.default.isAbsolute(relative));
}
