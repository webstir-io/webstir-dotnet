"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.buildConfig = buildConfig;
const path_1 = __importDefault(require("path"));
const constants_js_1 = require("../core/constants.js");
function buildConfig(workspaceRoot) {
    const srcRoot = path_1.default.join(workspaceRoot, constants_js_1.FOLDERS.src);
    const frontendRoot = path_1.default.join(srcRoot, constants_js_1.FOLDERS.frontend);
    const buildRoot = path_1.default.join(workspaceRoot, constants_js_1.FOLDERS.build);
    const distRoot = path_1.default.join(workspaceRoot, constants_js_1.FOLDERS.dist);
    const buildFrontend = path_1.default.join(buildRoot, constants_js_1.FOLDERS.frontend);
    const distFrontend = path_1.default.join(distRoot, constants_js_1.FOLDERS.frontend);
    return {
        version: 1,
        paths: {
            workspace: workspaceRoot,
            src: {
                root: srcRoot,
                frontend: frontendRoot,
                app: path_1.default.join(frontendRoot, constants_js_1.FOLDERS.app),
                pages: path_1.default.join(frontendRoot, constants_js_1.FOLDERS.pages),
                images: path_1.default.join(frontendRoot, constants_js_1.FOLDERS.images),
                fonts: path_1.default.join(frontendRoot, constants_js_1.FOLDERS.fonts),
                media: path_1.default.join(frontendRoot, constants_js_1.FOLDERS.media)
            },
            build: {
                root: buildRoot,
                frontend: buildFrontend,
                app: path_1.default.join(buildFrontend, constants_js_1.FOLDERS.app),
                pages: path_1.default.join(buildFrontend, constants_js_1.FOLDERS.pages),
                images: path_1.default.join(buildFrontend, constants_js_1.FOLDERS.images),
                fonts: path_1.default.join(buildFrontend, constants_js_1.FOLDERS.fonts),
                media: path_1.default.join(buildFrontend, constants_js_1.FOLDERS.media)
            },
            dist: {
                root: distRoot,
                frontend: distFrontend,
                app: path_1.default.join(distFrontend, constants_js_1.FOLDERS.app),
                pages: path_1.default.join(distFrontend, constants_js_1.FOLDERS.pages),
                images: path_1.default.join(distFrontend, constants_js_1.FOLDERS.images),
                fonts: path_1.default.join(distFrontend, constants_js_1.FOLDERS.fonts),
                media: path_1.default.join(distFrontend, constants_js_1.FOLDERS.media)
            }
        },
        features: {
            htmlSecurity: true,
            imageOptimization: true,
            precompression: true
        }
    };
}
