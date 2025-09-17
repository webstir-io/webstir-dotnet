import path from 'node:path';
import { load } from 'cheerio';
import type { CheerioAPI } from 'cheerio';
import { glob } from 'glob';
import { FOLDERS, FILES, FILE_NAMES, EXTENSIONS } from '../utils/constants.js';
import { ensureDir, readFile, writeFile, pathExists } from '../utils/fs.js';
import type { Builder, BuilderContext } from './types.js';
import { getPageDirectories } from '../utils/pages.js';
import { readPageManifest } from '../utils/assetManifest.js';
import { createCompressedVariants } from '../utils/precompression.js';
import { shouldProcess } from '../utils/changedFile.js';
import { getImageDimensions } from '../utils/imageOptimizer.js';
import { applyLazyLoading } from '../utils/lazyLoad.js';
import { addSubresourceIntegrity } from '../utils/htmlSecurity.js';
import { injectResourceHints } from '../utils/resourceHints.js';
import { inlineCriticalCss } from '../utils/criticalCss.js';
import { findPageFromChangedFile } from '../utils/pathMatch.js';

export function createHtmlBuilder(context: BuilderContext): Builder {
    return {
        name: 'html',
        async build(): Promise<void> {
            await buildHtml(context);
        },
        async publish(): Promise<void> {
            await publishHtml(context);
        }
    };
}

async function buildHtml(context: BuilderContext): Promise<void> {
    const { config } = context;
    if (!shouldProcess(context, [
        { directory: config.paths.src.pages, extensions: [EXTENSIONS.html] },
        { directory: config.paths.src.app, extensions: [EXTENSIONS.html] }
    ])) {
        return;
    }
    
    const appTemplatePath = path.join(config.paths.src.app, FILE_NAMES.htmlAppTemplate);
    if (!(await pathExists(appTemplatePath))) {
        throw new Error(`Missing base application template: ${appTemplatePath}`);
    }

    const templateHtml = await readFile(appTemplatePath);
    validateAppTemplate(templateHtml, appTemplatePath);

    const targetPage = findPageFromChangedFile(context.changedFile, config.paths.src.pages);
    const pages = await getPageDirectories(config.paths.src.pages);
    await ensureDir(config.paths.build.frontend);

    for (const page of pages) {
        if (targetPage && page.name !== targetPage) {
            continue;
        }
        const pageHtmlFiles = await glob('**/*.html', {
            cwd: page.directory,
            nodir: true
        });

        if (pageHtmlFiles.length === 0) {
            warn(`No HTML fragments found for page '${page.name}'.`);
            continue;
        }

        const targetDir = path.join(config.paths.build.frontend, FOLDERS.pages, page.name);
        await ensureDir(targetDir);

        for (const relativeHtml of pageHtmlFiles) {
            const sourceHtmlPath = path.join(page.directory, relativeHtml);
            const fragment = await readFile(sourceHtmlPath);
            validatePageFragment(fragment, sourceHtmlPath);

            const mergedHtml = mergeTemplates(templateHtml, fragment);
            const targetPath = path.join(targetDir, path.basename(relativeHtml));
            await writeFile(targetPath, mergedHtml);
        }
    }

    // Copy the app template for reference in the build output.
    const buildAppDir = path.join(config.paths.build.frontend, FOLDERS.app);
    await ensureDir(buildAppDir);
    await writeFile(path.join(buildAppDir, FILE_NAMES.htmlAppTemplate), templateHtml);
}

async function publishHtml(context: BuilderContext): Promise<void> {
    const { config } = context;
    const buildPagesRoot = path.join(config.paths.build.frontend, FOLDERS.pages);
    if (!(await pathExists(buildPagesRoot))) {
        warn('Skipping HTML publish because no build artifacts were found. Run build first.');
        return;
    }

    const targetPage = findPageFromChangedFile(context.changedFile, config.paths.src.pages);
    const pages = await getPageDirectories(buildPagesRoot);

    for (const page of pages) {
        if (targetPage && page.name !== targetPage) {
            continue;
        }
        const distDir = path.join(config.paths.dist.frontend, FOLDERS.pages, page.name);
        await ensureDir(distDir);

        const htmlFiles = await glob('**/*.html', {
            cwd: page.directory,
            nodir: true
        });

        const manifest = await readPageManifest(distDir, page.name);

        for (const relativeHtml of htmlFiles) {
            const sourcePath = path.join(page.directory, relativeHtml);
            const html = await readFile(sourcePath);
            const rewritten = await rewriteForPublish(context, html, page.name, manifest, page.directory);
            const outputPath = path.join(distDir, path.basename(relativeHtml));
            await writeFile(outputPath, rewritten);
            await createCompressedVariants(outputPath);
        }
    }
}

function mergeTemplates(appHtml: string, pageHtml: string): string {
    const app = load(appHtml);
    const page = load(pageHtml);

    const appMain = app('main').first();
    const pageMain = page('main').first();
    if (appMain.length === 0) {
        throw new Error('Base application template is missing a <main> element.');
    }
    if (pageMain.length === 0) {
        throw new Error('Page fragment is missing a <main> element.');
    }

    const appHead = app('head').first();
    const pageHead = page('head').first();
    if (appHead.length === 0 || pageHead.length === 0) {
        throw new Error('Templates must include a <head> element.');
    }

    appHead.append(pageHead.children());
    appMain.html(pageMain.html() ?? '');

    return app.root().html() ?? '';
}

async function rewriteForPublish(
    context: BuilderContext,
    html: string,
    pageName: string,
    manifest: { js?: string; css?: string },
    pageDirectory: string
): Promise<string> {
    const document = load(html);

    document(`script[src="/${FILES.refreshJs}"]`).remove();

    if (manifest.js) {
        const selector = `script[src="${FILES.index}${EXTENSIONS.js}"]`;
        document(selector).attr('src', `/${FOLDERS.pages}/${pageName}/${manifest.js}`);
        document(selector).attr('type', 'module');
    }

    if (manifest.css) {
        const selector = `link[href="${FILES.index}${EXTENSIONS.css}"]`;
        document(selector).attr('href', `/${FOLDERS.pages}/${pageName}/${manifest.css}`);
    }

    applyLazyLoading(document);
    await addImageDimensions(document, context, pageDirectory);
    await inlineCriticalCss(document, pageName, context.config.paths.dist.frontend, manifest.css);
    const sriResult = await addSubresourceIntegrity(document);
    if (sriResult.failures.length > 0) {
        for (const failure of sriResult.failures) {
            warn(`Failed to compute subresource integrity for ${failure}`);
        }
    }
    const hints = injectResourceHints(document, pageName);
    if (hints.missingHead) {
        warn('Unable to inject resource hints because <head> is missing.');
    }

    return document.root().html() ?? '';
}

function validateAppTemplate(html: string, filePath: string): void {
    const doc = load(html);
    if (doc('main').length === 0) {
        throw new Error(`Base template missing <main> container (${filePath}).`);
    }
    if (doc('head').length === 0) {
        throw new Error(`Base template missing <head> section (${filePath}).`);
    }
}

function validatePageFragment(html: string, filePath: string): void {
    const doc = load(html);
    if (doc('main').length === 0) {
        throw new Error(`Page fragment missing <main> section (${filePath}).`);
    }
    if (doc('head').length === 0) {
        throw new Error(`Page fragment missing <head> section (${filePath}).`);
    }
}

function warn(message: string): void {
    console.warn(`[webstir-frontend][html] ${message}`);
}

async function addImageDimensions(document: CheerioAPI, context: BuilderContext, pageDirectory: string): Promise<void> {
    const { config } = context;
    const images = document('img').toArray();

    await Promise.all(images.map(async (element) => {
        const img = document(element);
        if (img.attr('width') || img.attr('height')) {
            return;
        }

        const src = img.attr('src');
        if (!src || isExternalSource(src)) {
            return;
        }

        const assetPath = resolveAssetPath(src, pageDirectory, config.paths.build.frontend);
        if (!assetPath || !(await pathExists(assetPath))) {
            return;
        }

        const dimensions = await getImageDimensions(assetPath);
        if (!dimensions) {
            return;
        }

        img.attr('width', dimensions.width.toString());
        img.attr('height', dimensions.height.toString());
    }));
}

function isExternalSource(src: string): boolean {
    return src.startsWith('http://')
        || src.startsWith('https://')
        || src.startsWith('data:')
        || src.startsWith('//');
}

function resolveAssetPath(src: string, pageDirectory: string, buildRoot: string): string | null {
    const normalized = src.replace(/\\/g, '/');
    if (normalized.startsWith('/')) {
        const relative = normalized.replace(/^\//, '');
        return path.join(buildRoot, relative);
    }

    return path.join(pageDirectory, normalized);
}
