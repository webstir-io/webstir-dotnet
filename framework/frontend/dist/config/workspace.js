import fs from 'node:fs';
import path from 'path';
import { FOLDERS } from '../core/constants.js';
import { frontendFeatureFlagsSchema } from './schema.js';
const DEFAULT_FEATURE_FLAGS = {
    htmlSecurity: true,
    imageOptimization: true,
    precompression: true
};
export function buildConfig(workspaceRoot) {
    const srcRoot = path.join(workspaceRoot, FOLDERS.src);
    const frontendRoot = path.join(srcRoot, FOLDERS.frontend);
    const buildRoot = path.join(workspaceRoot, FOLDERS.build);
    const distRoot = path.join(workspaceRoot, FOLDERS.dist);
    const buildFrontend = path.join(buildRoot, FOLDERS.frontend);
    const distFrontend = path.join(distRoot, FOLDERS.frontend);
    return {
        version: 1,
        paths: {
            workspace: workspaceRoot,
            src: {
                root: srcRoot,
                frontend: frontendRoot,
                app: path.join(frontendRoot, FOLDERS.app),
                pages: path.join(frontendRoot, FOLDERS.pages),
                images: path.join(frontendRoot, FOLDERS.images),
                fonts: path.join(frontendRoot, FOLDERS.fonts),
                media: path.join(frontendRoot, FOLDERS.media)
            },
            build: {
                root: buildRoot,
                frontend: buildFrontend,
                app: path.join(buildFrontend, FOLDERS.app),
                pages: path.join(buildFrontend, FOLDERS.pages),
                images: path.join(buildFrontend, FOLDERS.images),
                fonts: path.join(buildFrontend, FOLDERS.fonts),
                media: path.join(buildFrontend, FOLDERS.media)
            },
            dist: {
                root: distRoot,
                frontend: distFrontend,
                app: path.join(distFrontend, FOLDERS.app),
                pages: path.join(distFrontend, FOLDERS.pages),
                images: path.join(distFrontend, FOLDERS.images),
                fonts: path.join(distFrontend, FOLDERS.fonts),
                media: path.join(distFrontend, FOLDERS.media)
            }
        },
        features: loadFeatureFlags(frontendRoot)
    };
}
function loadFeatureFlags(frontendRoot) {
    const configPath = path.join(frontendRoot, 'frontend.config.json');
    if (!fs.existsSync(configPath)) {
        return DEFAULT_FEATURE_FLAGS;
    }
    try {
        const raw = fs.readFileSync(configPath, 'utf8');
        const parsed = JSON.parse(raw);
        const overridesSource = extractOverrideSource(parsed);
        const overrides = frontendFeatureFlagsSchema.parse(overridesSource);
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
