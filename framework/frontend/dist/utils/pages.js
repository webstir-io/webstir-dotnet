"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.getPages = getPages;
exports.getPageDirectories = getPageDirectories;
const node_path_1 = __importDefault(require("node:path"));
const glob_1 = require("glob");
const fs_js_1 = require("./fs.js");
async function getPages(root) {
    const directories = await getPageDirectories(root);
    return directories.map((entry) => ({
        name: entry.name,
        directory: entry.directory
    }));
}
async function getPageDirectories(root) {
    if (!(await (0, fs_js_1.pathExists)(root))) {
        return [];
    }
    const entries = await (0, glob_1.glob)('*/', { cwd: root, absolute: false, withFileTypes: false });
    return entries.map((entry) => {
        const name = entry.endsWith('/') ? entry.slice(0, -1) : entry;
        return {
            name,
            directory: node_path_1.default.join(root, name)
        };
    });
}
