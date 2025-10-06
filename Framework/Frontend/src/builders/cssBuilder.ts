import path from 'node:path';
import postcss from 'postcss';
import autoprefixer from 'autoprefixer';
import * as cssoModule from 'csso';
import { glob } from 'glob';
import { FOLDERS, FILES, EXTENSIONS } from '../core/constants.js';
import { ensureDir, pathExists, readFile, writeFile, remove, copy } from '../utils/fs.js';
import type { Builder, BuilderContext } from './types.js';
import { getPages } from '../core/pages.js';
import { hashContent } from '../utils/hash.js';
import { updatePageManifest, updateSharedAssets, readSharedAssets } from '../assets/assetManifest.js';
import { createCompressedVariants } from '../assets/precompression.js';
import { shouldProcess } from '../utils/changedFile.js';
import { findPageFromChangedFile } from '../utils/pathMatch.js';

const MODULE_SUFFIX = '.module';
const APP_CSS_BASENAME = 'app';
const csso = ((cssoModule as unknown as { default?: typeof cssoModule }).default ?? cssoModule) as typeof cssoModule;

interface SharedCssArtifacts {
    appCss?: string;
}

export function createCssBuilder(context: BuilderContext): Builder {
    return {
        name: 'css',
        async build(): Promise<void> {
            await processCss(context, false);
        },
        async publish(): Promise<void> {
            await processCss(context, true);
        }
    };
}

async function processCss(context: BuilderContext, isProduction: boolean): Promise<void> {
    const { config } = context;
    if (!shouldProcess(context, [
        { directory: config.paths.src.pages, extensions: [EXTENSIONS.css] },
        { directory: config.paths.src.frontend, extensions: [EXTENSIONS.css] }
    ])) {
        return;
    }

    const sharedArtifacts = await processAppCss(config, isProduction);
    const targetPage = findPageFromChangedFile(context.changedFile, config.paths.src.pages);
    const pages = await getPages(config.paths.src.pages);

    for (const page of pages) {
        if (targetPage && page.name !== targetPage) {
            continue;
        }
        const entryPath = await resolveCssEntry(page.directory);
        if (!entryPath) {
            continue;
        }

        const css = await readFile(entryPath);
        const processor = postcss([autoprefixer]);
        const processed = await processor.process(css, { from: entryPath, map: !isProduction ? { inline: true } : false });
        const normalized = resolveAppImports(processed.css, isProduction ? sharedArtifacts.appCss : undefined);

        if (isProduction) {
            const inlined = await inlineAppImports(normalized, config.paths.dist.frontend);
            await emitProductionCss(config, page.name, inlined);
        } else {
            await emitDevelopmentCss(config, page.name, normalized);
        }
    }
}

async function emitDevelopmentCss(config: BuilderContext['config'], pageName: string, css: string): Promise<void> {
    const outputDir = path.join(config.paths.build.frontend, FOLDERS.pages, pageName);
    await ensureDir(outputDir);
    const outputPath = path.join(outputDir, `${FILES.index}${EXTENSIONS.css}`);
    await writeFile(outputPath, css);
}

async function emitProductionCss(config: BuilderContext['config'], pageName: string, css: string): Promise<void> {
    const minified = csso.minify(css).css;
    const hash = hashContent(minified);
    const fileName = `${FILES.index}-${hash}${EXTENSIONS.css}`;
    const outputDir = path.join(config.paths.dist.frontend, FOLDERS.pages, pageName);
    await ensureDir(outputDir);
    const outputPath = path.join(outputDir, fileName);
    await writeFile(outputPath, minified);
    if (config.features.precompression) {
        await createCompressedVariants(outputPath);
    } else {
        await Promise.all([
            remove(`${outputPath}${EXTENSIONS.br}`).catch(() => undefined),
            remove(`${outputPath}${EXTENSIONS.gz}`).catch(() => undefined)
        ]);
    }
    await updatePageManifest(outputDir, pageName, (manifest) => {
        manifest.css = fileName;
    });
}

async function processAppCss(config: BuilderContext['config'], isProduction: boolean): Promise<SharedCssArtifacts> {
    const appCssPath = path.join(config.paths.src.app, 'app.css');
    if (!(await pathExists(appCssPath))) {
        return {};
    }

    const processor = postcss([autoprefixer]);
    const source = await readFile(appCssPath);

    if (isProduction) {
        const stylesMap = await emitAppStylesProduction(config, processor);
        const processed = await processor.process(source, { from: appCssPath, map: false });
        const rewritten = rewriteAppStyleImports(processed.css, stylesMap);
        const fileName = await emitAppProductionCss(config, rewritten);
        await updateSharedAssets(config.paths.dist.frontend, shared => {
            shared.css = fileName;
        });
        return { appCss: fileName };
    }

    const processed = await processor.process(source, { from: appCssPath, map: { inline: true } });
    await emitAppDevelopmentCss(config, processed.css);
    await syncAppStyles(config.paths.src.app, path.join(config.paths.build.frontend, FOLDERS.app));
    return {};
}

async function emitAppDevelopmentCss(config: BuilderContext['config'], css: string): Promise<void> {
    const outputDir = path.join(config.paths.build.frontend, FOLDERS.app);
    await ensureDir(outputDir);
    await writeFile(path.join(outputDir, 'app.css'), css);
}

async function emitAppProductionCss(config: BuilderContext['config'], css: string): Promise<string> {
    const minified = csso.minify(css).css;
    const hash = hashContent(minified);
    const fileName = `${APP_CSS_BASENAME}-${hash}${EXTENSIONS.css}`;
    const outputDir = path.join(config.paths.dist.frontend, FOLDERS.app);
    await ensureDir(outputDir);
    const outputPath = path.join(outputDir, fileName);
    await writeFile(outputPath, minified);

    if (config.features.precompression) {
        await createCompressedVariants(outputPath);
    } else {
        await Promise.all([
            remove(`${outputPath}${EXTENSIONS.br}`).catch(() => undefined),
            remove(`${outputPath}${EXTENSIONS.gz}`).catch(() => undefined)
        ]);
    }

    // Remove previously hashed variants to avoid stale files.
    const existing = await readSharedAssets(config.paths.dist.frontend);
    const previousFile = existing?.css;
    if (previousFile && previousFile !== fileName) {
        const previousPath = path.join(outputDir, previousFile);
        await remove(previousPath).catch(() => undefined);
        await remove(`${previousPath}${EXTENSIONS.br}`).catch(() => undefined);
        await remove(`${previousPath}${EXTENSIONS.gz}`).catch(() => undefined);
    }

    return fileName;
}

async function syncAppStyles(sourceAppDir: string, destinationAppDir: string): Promise<void> {
    const stylesSource = path.join(sourceAppDir, 'styles');
    if (!(await pathExists(stylesSource))) {
        return;
    }

    const stylesDestination = path.join(destinationAppDir, 'styles');
    await ensureDir(path.dirname(stylesDestination));
    await copy(stylesSource, stylesDestination);
}

function resolveAppImports(css: string, appCssFile?: string): string {
    let result = css;

    if (appCssFile) {
        result = result.replace(/@import\s+['"]@app\/app\.css['"];?/g, `@import "/app/${appCssFile}";`);
    }

    return result.replace(/@app\//g, '/app/');
}

async function inlineAppImports(css: string, distRoot: string, seen: Set<string> = new Set()): Promise<string> {
    const importPattern = /@import\s+(?:url\()?[\s]*['"]\/app\/([^'"\)]+)['"][\s]*\)?;?/g;
    const segments: string[] = [];
    let lastIndex = 0;

    for (const match of css.matchAll(importPattern)) {
        const index = match.index ?? 0;
        segments.push(css.slice(lastIndex, index));

        const relative = normalizeForwardSlashes(match[1] ?? '');
        const inlined = await inlineAppImport(relative, distRoot, seen);
        if (inlined !== null) {
            segments.push(inlined);
        } else {
            segments.push(match[0]);
        }

        lastIndex = index + match[0].length;
    }

    segments.push(css.slice(lastIndex));
    return segments.join('');
}

async function inlineAppImport(relativePath: string, distRoot: string, seen: Set<string>): Promise<string | null> {
    if (relativePath.length === 0 || relativePath.includes('..')) {
        return null;
    }

    const resolved = path.join(distRoot, FOLDERS.app, relativePath);
    if (!(await pathExists(resolved))) {
        return null;
    }

    const key = resolved;
    if (seen.has(key)) {
        return '';
    }

    seen.add(key);
    const content = await readFile(resolved);
    const inlined = await inlineAppImports(content, distRoot, seen);
    seen.delete(key);

    return inlined;
}

async function emitAppStylesProduction(
    config: BuilderContext['config'],
    processor: postcss.Processor
): Promise<Map<string, string>> {
    const sourceDir = path.join(config.paths.src.app, 'styles');
    const mapping = new Map<string, string>();

    if (!(await pathExists(sourceDir))) {
        const destinationDir = path.join(config.paths.dist.frontend, FOLDERS.app, 'styles');
        await remove(destinationDir).catch(() => undefined);
        return mapping;
    }

    const destinationDir = path.join(config.paths.dist.frontend, FOLDERS.app, 'styles');
    await remove(destinationDir).catch(() => undefined);

    const files = await glob('**/*.css', { cwd: sourceDir, nodir: true });
    for (const relative of files) {
        const sourcePath = path.join(sourceDir, relative);
        const processed = await processor.process(await readFile(sourcePath), { from: sourcePath, map: false });
        const minified = csso.minify(processed.css).css;
        const hash = hashContent(minified);
        const parsed = path.parse(relative);
        const hashedName = `${parsed.name}-${hash}${EXTENSIONS.css}`;
        const relativeHashedPath = parsed.dir ? path.join(parsed.dir, hashedName) : hashedName;
        const destinationPath = path.join(destinationDir, relativeHashedPath);
        await ensureDir(path.dirname(destinationPath));
        await writeFile(destinationPath, minified);

        if (config.features.precompression) {
            await createCompressedVariants(destinationPath);
        } else {
            await Promise.all([
                remove(`${destinationPath}${EXTENSIONS.br}`).catch(() => undefined),
                remove(`${destinationPath}${EXTENSIONS.gz}`).catch(() => undefined)
            ]);
        }

        mapping.set(normalizeForwardSlashes(relative), normalizeForwardSlashes(path.join('styles', relativeHashedPath)));
    }

    return mapping;
}

function rewriteAppStyleImports(css: string, stylesMap: Map<string, string>): string {
    if (stylesMap.size === 0) {
        return css;
    }

    let result = css;
    for (const [original, hashed] of stylesMap.entries()) {
        const normalizedOriginal = original.startsWith('styles/') ? original : `styles/${original}`;
        const escaped = escapeRegExp(normalizedOriginal);
        const pattern = new RegExp(`(@import\\s+['"])(?:\.\/)?${escaped}(['"];?)`, 'g');
        result = result.replace(pattern, `$1/app/${hashed}$2`);
    }

    return result;
}

function normalizeForwardSlashes(value: string): string {
    return value.replace(/\\/g, '/');
}

function escapeRegExp(value: string): string {
    return value.replace(/[.*+?^${}()|[\\]\\]/g, '\\$&');
}

async function resolveCssEntry(pageDirectory: string): Promise<string | null> {
    const modulePath = path.join(pageDirectory, `${FILES.index}${MODULE_SUFFIX}${EXTENSIONS.css}`);
    if (await pathExists(modulePath)) {
        return modulePath;
    }

    const plainPath = path.join(pageDirectory, `${FILES.index}${EXTENSIONS.css}`);
    if (await pathExists(plainPath)) {
        return plainPath;
    }

    return null;
}
