import path from 'node:path';
import { FOLDERS, EXTENSIONS } from '../core/constants.js';
import { copy, pathExists, emptyDir, ensureDir, remove } from '../utils/fs.js';
import type { Builder, BuilderContext } from './types.js';
import { shouldProcess } from '../utils/changedFile.js';
import { optimizeImages } from '../assets/imageOptimizer.js';
import { relativePathWithin } from '../utils/pathMatch.js';

const IMAGE_EXTENSIONS = [
    EXTENSIONS.png,
    EXTENSIONS.jpg,
    EXTENSIONS.jpeg,
    EXTENSIONS.gif,
    EXTENSIONS.svg,
    EXTENSIONS.webp,
    EXTENSIONS.ico
] as const;

const FONT_EXTENSIONS = [
    EXTENSIONS.woff,
    EXTENSIONS.woff2,
    EXTENSIONS.ttf,
    EXTENSIONS.otf,
    EXTENSIONS.eot
] as const;

const MEDIA_EXTENSIONS = [
    EXTENSIONS.mp3,
    EXTENSIONS.m4a,
    EXTENSIONS.wav,
    EXTENSIONS.ogg,
    EXTENSIONS.mp4,
    EXTENSIONS.webm,
    EXTENSIONS.mov
] as const;

export function createStaticAssetsBuilder(context: BuilderContext): Builder {
    return {
        name: 'static-assets',
        async build(): Promise<void> {
            await copyStaticAssets(context, false);
        },
        async publish(): Promise<void> {
            await copyStaticAssets(context, true);
        }
    };
}

async function copyStaticAssets(context: BuilderContext, isProduction: boolean): Promise<void> {
    const { config } = context;
    if (!shouldProcess(context, [
        { directory: config.paths.src.images, extensions: IMAGE_EXTENSIONS },
        { directory: config.paths.src.fonts, extensions: FONT_EXTENSIONS },
        { directory: config.paths.src.media, extensions: MEDIA_EXTENSIONS }
    ])) {
        return;
    }

    const targets = [
        { source: config.paths.src.images, build: config.paths.build.frontend, dist: config.paths.dist.frontend, folder: FOLDERS.images, extensions: IMAGE_EXTENSIONS },
        { source: config.paths.src.fonts, build: config.paths.build.frontend, dist: config.paths.dist.frontend, folder: FOLDERS.fonts, extensions: FONT_EXTENSIONS },
        { source: config.paths.src.media, build: config.paths.build.frontend, dist: config.paths.dist.frontend, folder: FOLDERS.media, extensions: MEDIA_EXTENSIONS }
    ];

    for (const target of targets) {
        if (!(await pathExists(target.source))) {
            continue;
        }

        const changedRelative = relativePathWithin(context.changedFile, target.source);
        const buildDestination = path.join(target.build, target.folder);

        if (!context.changedFile || !changedRelative) {
            await emptyDir(buildDestination);
            await copy(target.source, buildDestination);

            if (isProduction) {
                const distDestination = path.join(target.dist, target.folder);
                if (target.folder === FOLDERS.images) {
                    await optimizeImages(buildDestination, distDestination);
                } else {
                    await emptyDir(distDestination);
                    await copy(buildDestination, distDestination);
                }
            }
            continue;
        }

        await copySingleAsset(target.source, buildDestination, changedRelative);

        if (isProduction) {
            const distDestination = path.join(target.dist, target.folder);
            if (target.folder === FOLDERS.images) {
                await optimizeImages(buildDestination, distDestination, [changedRelative]);
            } else {
                const sourcePath = path.join(target.source, changedRelative);
                const destPath = path.join(distDestination, changedRelative);
                if (await pathExists(sourcePath)) {
                    await ensureDir(path.dirname(destPath));
                    await copy(sourcePath, destPath);
                } else {
                    await remove(destPath).catch(() => undefined);
                }
            }
        }
    }
}

async function copySingleAsset(sourceRoot: string, buildRoot: string, relativePath: string): Promise<void> {
    const sourcePath = path.join(sourceRoot, relativePath);
    const destinationPath = path.join(buildRoot, relativePath);

    if (await pathExists(sourcePath)) {
        await ensureDir(path.dirname(destinationPath));
        await copy(sourcePath, destinationPath);
    } else {
        await remove(destinationPath).catch(() => undefined);
    }
}
