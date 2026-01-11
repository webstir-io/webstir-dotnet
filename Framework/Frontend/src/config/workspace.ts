import fs from 'node:fs';
import path from 'path';
import type { FrontendConfig, FrontendFeatureFlags, FrontendPublishConfig } from '../types.js';
import { FOLDERS } from '../core/constants.js';
import { frontendFeatureFlagsSchema, frontendPublishSchema } from './schema.js';
import { normalizeBasePath } from '../utils/publicPath.js';

const DEFAULT_FEATURE_FLAGS: FrontendFeatureFlags = {
    htmlSecurity: true,
    imageOptimization: true,
    precompression: true
};

const DEFAULT_PUBLISH_CONFIG: FrontendPublishConfig = {
    basePath: ''
};

export function buildConfig(workspaceRoot: string): FrontendConfig {
    const srcRoot = path.join(workspaceRoot, FOLDERS.src);
    const frontendRoot = path.join(srcRoot, FOLDERS.frontend);
    const buildRoot = path.join(workspaceRoot, FOLDERS.build);
    const distRoot = path.join(workspaceRoot, FOLDERS.dist);

    const buildFrontend = path.join(buildRoot, FOLDERS.frontend);
    const distFrontend = path.join(distRoot, FOLDERS.frontend);

    const overrides = loadConfigOverrides(frontendRoot);
    const workspaceMode = readWorkspaceMode(workspaceRoot);
    const publishOverrides = workspaceMode === 'ssg'
        ? overrides.publish
        : DEFAULT_PUBLISH_CONFIG;

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
        features: overrides.features,
        publish: publishOverrides
    };
}

interface FrontendConfigOverrides {
    features: FrontendFeatureFlags;
    publish: FrontendPublishConfig;
}

function loadConfigOverrides(frontendRoot: string): FrontendConfigOverrides {
    const configPath = path.join(frontendRoot, 'frontend.config.json');
    if (!fs.existsSync(configPath)) {
        return {
            features: DEFAULT_FEATURE_FLAGS,
            publish: DEFAULT_PUBLISH_CONFIG
        };
    }

    try {
        const raw = fs.readFileSync(configPath, 'utf8');
        const parsed = JSON.parse(raw) as unknown;
        const featureSource = extractOverrideSource(parsed, 'features');
        const publishSource = extractOverrideSource(parsed, 'publish');
        const overrides = frontendFeatureFlagsSchema.parse(featureSource);
        const publishOverrides = frontendPublishSchema.parse(publishSource);
        return {
            features: {
                htmlSecurity: overrides.htmlSecurity,
                imageOptimization: overrides.imageOptimization,
                precompression: overrides.precompression
            },
            publish: {
                basePath: normalizeBasePath(publishOverrides.basePath)
            }
        };
    } catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        throw new Error(`Failed to read frontend config overrides from ${configPath}: ${message}`);
    }
}

function extractOverrideSource(value: unknown, key: string): Record<string, unknown> {
    if (value && typeof value === 'object' && key in (value as Record<string, unknown>)) {
        const container = (value as Record<string, unknown>)[key];
        if (container && typeof container === 'object') {
            return container as Record<string, unknown>;
        }
        return {};
    }

    return (value && typeof value === 'object') ? value as Record<string, unknown> : {};
}

function readWorkspaceMode(workspaceRoot: string): string | null {
    const packageJsonPath = path.join(workspaceRoot, 'package.json');
    if (!fs.existsSync(packageJsonPath)) {
        return null;
    }

    try {
        const raw = fs.readFileSync(packageJsonPath, 'utf8');
        const parsed = JSON.parse(raw) as Record<string, unknown>;
        const webstir = parsed?.webstir as Record<string, unknown> | undefined;
        const mode = typeof webstir?.mode === 'string' ? webstir.mode.trim().toLowerCase() : '';
        return mode || null;
    } catch {
        return null;
    }
}
