"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.writeConfigManifest = writeConfigManifest;
exports.readConfigManifest = readConfigManifest;
const fs_1 = require("fs");
const path_1 = __importDefault(require("path"));
const schema_js_1 = require("./schema.js");
async function writeConfigManifest(options) {
    const parsed = schema_js_1.frontendConfigSchema.parse(options.data);
    const directory = path_1.default.dirname(options.outputPath);
    await fs_1.promises.mkdir(directory, { recursive: true });
    const serialized = JSON.stringify(parsed, undefined, 2);
    const tempPath = path_1.default.join(directory, `.webstir-frontend-${process.pid}-${Date.now()}.tmp`);
    await fs_1.promises.writeFile(tempPath, serialized, 'utf8');
    await fs_1.promises.rename(tempPath, options.outputPath);
}
async function readConfigManifest(manifestPath) {
    const json = await fs_1.promises.readFile(manifestPath, 'utf8');
    const parsed = JSON.parse(json);
    return schema_js_1.frontendConfigSchema.parse(parsed);
}
