import path from 'path';
import type { FrontendConfig } from '../types.js';
import { FOLDERS } from '../core/constants.js';

export function buildConfig(workspaceRoot: string): FrontendConfig {
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
        features: {
            htmlSecurity: true,
            imageOptimization: true,
            precompression: true
        }
    };
}
