"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.ensureDir = ensureDir;
exports.emptyDir = emptyDir;
exports.remove = remove;
exports.copy = copy;
exports.pathExists = pathExists;
exports.stat = stat;
exports.readJson = readJson;
exports.writeJson = writeJson;
exports.readFile = readFile;
exports.writeFile = writeFile;
const fs_extra_1 = __importDefault(require("fs-extra"));
async function ensureDir(path) {
    await fs_extra_1.default.ensureDir(path);
}
async function emptyDir(path) {
    await fs_extra_1.default.emptyDir(path);
}
async function remove(path) {
    await fs_extra_1.default.remove(path);
}
async function copy(source, destination) {
    await fs_extra_1.default.copy(source, destination, { overwrite: true, errorOnExist: false });
}
async function pathExists(path) {
    return fs_extra_1.default.pathExists(path);
}
async function stat(path) {
    return fs_extra_1.default.stat(path);
}
async function readJson(path) {
    try {
        return await fs_extra_1.default.readJson(path);
    }
    catch (error) {
        if (error.code === 'ENOENT') {
            return null;
        }
        throw error;
    }
}
async function writeJson(path, data) {
    await fs_extra_1.default.writeJson(path, data, { spaces: 2 });
}
async function readFile(path) {
    return fs_extra_1.default.readFile(path, 'utf8');
}
async function writeFile(path, contents) {
    await fs_extra_1.default.outputFile(path, contents, 'utf8');
}
