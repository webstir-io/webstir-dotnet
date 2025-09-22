"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.buildConfig = buildConfig;
const node_fs_1 = __importDefault(require("node:fs"));
const path_1 = __importDefault(require("path"));
const constants_js_1 = require("../core/constants.js");
const schema_js_1 = require("./schema.js");
const DEFAULT_FEATURE_FLAGS = {
    htmlSecurity: true,
    imageOptimization: true,
    precompression: true
};
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
        features: loadFeatureFlags(frontendRoot)
    };
}
function loadFeatureFlags(frontendRoot) {
    const configPath = path_1.default.join(frontendRoot, 'frontend.config.json');
    if (!node_fs_1.default.existsSync(configPath)) {
        return DEFAULT_FEATURE_FLAGS;
    }
    try {
        const raw = node_fs_1.default.readFileSync(configPath, 'utf8');
        const parsed = JSON.parse(raw);
        const overridesSource = extractOverrideSource(parsed);
        const overrides = schema_js_1.frontendFeatureFlagsSchema.parse(overridesSource);
        return {
            htmlSecurity: overrides.htmlSecurity,
            imageOptimization: overrides.imageOptimization,
            precompression: overrides.precompression
        };
    }
    catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        throw new Error(`Failed to read frontend feature flags from ${configPath}: ${message}`);
    }
}
function extractOverrideSource(value) {
    if (value && typeof value === 'object' && 'features' in value) {
        const container = value.features;
        if (container && typeof container === 'object') {
            return container;
        }
    }
    return (value && typeof value === 'object') ? value : {};
}
