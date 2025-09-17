import path from 'node:path';
import sharp from 'sharp';
import { glob } from 'glob';
import { copy, ensureDir, emptyDir, pathExists, remove } from '../utils/fs.js';
import { EXTENSIONS } from '../core/constants.js';

const TRANSCODABLE_EXTENSIONS = new Set<string>([
    EXTENSIONS.png,
    EXTENSIONS.jpg,
    EXTENSIONS.jpeg
]);

export interface ImageDimensions
{
    width: number;
    height: number;
}

export async function optimizeImages(sourceDir: string, destinationDir: string, files?: string[]): Promise<void> {
    if (!(await pathExists(sourceDir))) {
        await emptyDir(destinationDir);
        return;
    }

    if (!files || files.length === 0) {
        await emptyDir(destinationDir);
        const allFiles = await glob('**/*', { cwd: sourceDir, nodir: true });
        await Promise.all(allFiles.map(async (relative) => processImage(sourceDir, destinationDir, relative)));
        return;
    }

    await ensureDir(destinationDir);
    await Promise.all(files.map(async (relative) => processImage(sourceDir, destinationDir, relative, true)));
}

export async function getImageDimensions(filePath: string): Promise<ImageDimensions | null> {
    try {
        const metadata = await sharp(filePath).metadata();
        if (typeof metadata.width === 'number' && typeof metadata.height === 'number') {
            return { width: metadata.width, height: metadata.height };
        }
    } catch {
        // Ignore errors – the caller can continue without dimensions.
    }
    return null;
}

function replaceExtension(filePath: string, extension: string): string {
    const parsed = path.parse(filePath);
    return path.join(parsed.dir, `${parsed.name}${extension}`);
}

async function createWebpVariant(sourcePath: string, destinationPath: string): Promise<void> {
    try {
        await sharp(sourcePath)
            .webp({ quality: 75 })
            .toFile(destinationPath);
    } catch {
        // Ignore failures; fall back to original image only.
    }
}

async function createAvifVariant(sourcePath: string, destinationPath: string): Promise<void> {
    try {
        await sharp(sourcePath)
            .avif({ quality: 45 })
            .toFile(destinationPath);
    } catch {
        // Ignore failures; fall back to original image only.
    }
}

async function processImage(sourceDir: string, destinationDir: string, relative: string, incremental = false): Promise<void> {
    const sourcePath = path.join(sourceDir, relative);
    const destinationPath = path.join(destinationDir, relative);

    if (!(await pathExists(sourcePath))) {
        await removeVariants(destinationPath, true);
        return;
    }

    await ensureDir(path.dirname(destinationPath));
    await copy(sourcePath, destinationPath);

    const extension = path.extname(sourcePath).toLowerCase();
    if (!TRANSCODABLE_EXTENSIONS.has(extension)) {
        if (incremental) {
            await removeVariants(destinationPath, false);
        }
        return;
    }

    if (incremental) {
        await removeVariants(destinationPath, false);
    }

    await Promise.all([
        createWebpVariant(sourcePath, replaceExtension(destinationPath, EXTENSIONS.webp)),
        createAvifVariant(sourcePath, replaceExtension(destinationPath, EXTENSIONS.avif))
    ]);
}

async function removeVariants(destinationPath: string, includeBase: boolean): Promise<void> {
    const targets = [replaceExtension(destinationPath, EXTENSIONS.webp), replaceExtension(destinationPath, EXTENSIONS.avif)];
    if (includeBase) {
        targets.push(destinationPath);
    }

    await Promise.all(targets.map(async (target) => {
        await remove(target).catch(() => undefined);
    }));
}
