"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.inlineCriticalCss = inlineCriticalCss;
const node_path_1 = __importDefault(require("node:path"));
const constants_js_1 = require("../core/constants.js");
const fs_js_1 = require("../utils/fs.js");
const INLINE_THRESHOLD_BYTES = 6 * 1024;
async function inlineCriticalCss(document, pageName, distRoot, cssFile) {
    if (!cssFile) {
        return;
    }
    const cssPath = node_path_1.default.join(distRoot, constants_js_1.FOLDERS.pages, pageName, cssFile);
    if (!(await (0, fs_js_1.pathExists)(cssPath))) {
        return;
    }
    const info = await (0, fs_js_1.stat)(cssPath).catch(() => null);
    if (!info || !info.isFile() || info.size > INLINE_THRESHOLD_BYTES) {
        return;
    }
    const cssContent = await (0, fs_js_1.readFile)(cssPath);
    const head = document('head').first();
    if (head.length === 0) {
        return;
    }
    const href = `/${constants_js_1.FOLDERS.pages}/${pageName}/${cssFile}`;
    document(`link[href="${href}"]`).remove();
    if (cssFile.endsWith(constants_js_1.EXTENSIONS.css)) {
        document(`link[rel="preload"][href="${href}"]`).remove();
    }
    head.append(`\n<style data-critical>\n${cssContent}\n</style>\n`);
}
