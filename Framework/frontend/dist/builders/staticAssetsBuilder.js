import path from 'node:path';
import { FOLDERS, EXTENSIONS, FILES } from '../core/constants.js';
import { copy, pathExists, emptyDir, ensureDir, remove, writeFile } from '../utils/fs.js';
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
];
const FONT_EXTENSIONS = [
    EXTENSIONS.woff,
    EXTENSIONS.woff2,
    EXTENSIONS.ttf,
    EXTENSIONS.otf,
    EXTENSIONS.eot
];
const MEDIA_EXTENSIONS = [
    EXTENSIONS.mp3,
    EXTENSIONS.m4a,
    EXTENSIONS.wav,
    EXTENSIONS.ogg,
    EXTENSIONS.mp4,
    EXTENSIONS.webm,
    EXTENSIONS.mov
];
const ALLOW_ALL_ROBOTS = 'User-agent: *\nAllow: /\n';
export function createStaticAssetsBuilder(context) {
    return {
        name: 'static-assets',
        async build() {
            await copyStaticAssets(context, false);
        },
        async publish() {
            await copyStaticAssets(context, true);
        }
    };
}
async function copyStaticAssets(context, isProduction) {
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
                    if (config.features.imageOptimization) {
                        await optimizeImages(buildDestination, distDestination);
                    }
                    else {
                        await emptyDir(distDestination);
                        await copy(buildDestination, distDestination);
                    }
                }
                else {
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
                if (config.features.imageOptimization) {
                    await optimizeImages(buildDestination, distDestination, [changedRelative]);
                }
                else {
                    await syncImageWithoutOptimization(buildDestination, distDestination, changedRelative);
                }
            }
            else {
                const sourcePath = path.join(target.source, changedRelative);
                const destPath = path.join(distDestination, changedRelative);
                if (await pathExists(sourcePath)) {
                    await ensureDir(path.dirname(destPath));
                    await copy(sourcePath, destPath);
                }
                else {
                    await remove(destPath).catch(() => undefined);
                }
            }
        }
    }
    await syncRobotsTxt(config, isProduction);
}
async function copySingleAsset(sourceRoot, buildRoot, relativePath) {
    const sourcePath = path.join(sourceRoot, relativePath);
    const destinationPath = path.join(buildRoot, relativePath);
    if (await pathExists(sourcePath)) {
        await ensureDir(path.dirname(destinationPath));
        await copy(sourcePath, destinationPath);
    }
    else {
        await remove(destinationPath).catch(() => undefined);
    }
}
async function syncImageWithoutOptimization(buildRoot, distRoot, relativePath) {
    const sourcePath = path.join(buildRoot, relativePath);
    const destinationPath = path.join(distRoot, relativePath);
    if (await pathExists(sourcePath)) {
        await ensureDir(path.dirname(destinationPath));
        await copy(sourcePath, destinationPath);
    }
    else {
        await remove(destinationPath).catch(() => undefined);
    }
    await Promise.all([
        remove(`${destinationPath}${EXTENSIONS.webp}`).catch(() => undefined),
        remove(`${destinationPath}${EXTENSIONS.avif}`).catch(() => undefined)
    ]);
}
async function syncRobotsTxt(config, isProduction) {
    const sourcePath = path.join(config.paths.src.frontend, FILES.robotsTxt);
    const buildPath = path.join(config.paths.build.frontend, FILES.robotsTxt);
    if (await pathExists(sourcePath)) {
        await ensureDir(path.dirname(buildPath));
        await copy(sourcePath, buildPath);
        if (isProduction) {
            const distPath = path.join(config.paths.dist.frontend, FILES.robotsTxt);
            await ensureDir(path.dirname(distPath));
            await copy(sourcePath, distPath);
        }
    }
    else {
        await ensureDir(path.dirname(buildPath));
        await writeFile(buildPath, ALLOW_ALL_ROBOTS);
        if (isProduction) {
            const distPath = path.join(config.paths.dist.frontend, FILES.robotsTxt);
            await ensureDir(path.dirname(distPath));
            await writeFile(distPath, ALLOW_ALL_ROBOTS);
        }
    }
}
