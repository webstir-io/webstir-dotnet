import path from 'node:path';
import { load } from 'cheerio';
import { glob } from 'glob';
import { minify } from 'html-minifier-terser';
import { FOLDERS, FILES, FILE_NAMES, EXTENSIONS } from '../core/constants.js';
import { ensureDir, readFile, writeFile, pathExists, remove } from '../utils/fs.js';
import { getPageDirectories } from '../core/pages.js';
import { readPageManifest, readSharedAssets } from '../assets/assetManifest.js';
import { createCompressedVariants } from '../assets/precompression.js';
import { shouldProcess } from '../utils/changedFile.js';
import { getImageDimensions } from '../assets/imageOptimizer.js';
import { applyLazyLoading } from '../html/lazyLoad.js';
import { addSubresourceIntegrity } from '../html/htmlSecurity.js';
import { injectResourceHints } from '../html/resourceHints.js';
import { inlineCriticalCss } from '../html/criticalCss.js';
import { findPageFromChangedFile } from '../utils/pathMatch.js';
import { emitDiagnostic } from '../core/diagnostics.js';
export function createHtmlBuilder(context) {
    return {
        name: 'html',
        async build() {
            await buildHtml(context);
        },
        async publish() {
            await publishHtml(context);
        }
    };
}
async function buildHtml(context) {
    const { config } = context;
    if (!shouldProcess(context, [
        { directory: config.paths.src.pages, extensions: [EXTENSIONS.html] },
        { directory: config.paths.src.app, extensions: [EXTENSIONS.html] }
    ])) {
        return;
    }
    const appTemplatePath = path.join(config.paths.src.app, FILE_NAMES.htmlAppTemplate);
    if (!(await pathExists(appTemplatePath))) {
        throw new Error(`Base application HTML file not found: ${appTemplatePath}`);
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
async function publishHtml(context) {
    const { config } = context;
    const buildPagesRoot = path.join(config.paths.build.frontend, FOLDERS.pages);
    if (!(await pathExists(buildPagesRoot))) {
        warn('Skipping HTML publish because no build artifacts were found. Run build first.');
        return;
    }
    const targetPage = findPageFromChangedFile(context.changedFile, config.paths.src.pages);
    const pages = await getPageDirectories(buildPagesRoot);
    const shared = await readSharedAssets(config.paths.dist.frontend);
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
            const rewritten = await rewriteForPublish(context, html, page.name, manifest, page.directory, shared);
            const outputPath = path.join(distDir, path.basename(relativeHtml));
            await writeFile(outputPath, rewritten);
            await handlePrecompression(context, outputPath);
        }
    }
}
function mergeTemplates(appHtml, pageHtml) {
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
async function rewriteForPublish(context, html, pageName, manifest, pageDirectory, shared) {
    const document = load(html);
    removeDevScripts(document);
    if (shared?.css) {
        document(`link[href="/app/app.css"]`).attr('href', `/app/${shared.css}`);
    }
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
    if (context.config.features.imageOptimization) {
        await addImageDimensions(document, context, pageDirectory);
    }
    if (context.config.features.htmlSecurity) {
        await inlineCriticalCss(document, pageName, context.config.paths.dist.frontend, manifest.css);
        const sriResult = await addSubresourceIntegrity(document);
        if (sriResult.failures.length > 0) {
            const resources = sriResult.failures;
            const message = resources.length === 1
                ? `Failed to compute subresource integrity for ${resources[0]}.`
                : `Failed to compute subresource integrity for ${resources.length} resources.`;
            emitDiagnostic({
                code: 'frontend.sri.unresolved',
                kind: 'sri',
                stage: 'html.publish',
                severity: 'warning',
                message,
                data: { resources },
                suggestion: 'Verify the resource is reachable and not blocked by auth or network constraints.'
            });
        }
        const hints = injectResourceHints(document, pageName);
        if (hints.missingHead) {
            emitDiagnostic({
                code: 'frontend.resourceHints.missingHead',
                kind: 'resource-hints',
                stage: 'html.publish',
                severity: 'warning',
                message: 'Unable to inject resource hints because <head> is missing.',
                data: { candidates: hints.candidates }
            });
        }
    }
    dedupeHeadMeta(document, 'name');
    dedupeHeadMeta(document, 'property');
    dedupeHeadLinks(document, 'rel');
    const htmlOutput = document.root().html() ?? '';
    return await minifyHtml(htmlOutput);
}
async function handlePrecompression(context, outputPath) {
    if (context.config.features.precompression) {
        await createCompressedVariants(outputPath);
        return;
    }
    await Promise.all([
        remove(`${outputPath}${EXTENSIONS.br}`).catch(() => undefined),
        remove(`${outputPath}${EXTENSIONS.gz}`).catch(() => undefined)
    ]);
}
function validateAppTemplate(html, filePath) {
    const doc = load(html);
    if (doc('main').length === 0) {
        throw new Error(`Base template missing <main> container (${filePath}).`);
    }
    if (doc('head').length === 0) {
        throw new Error(`Base template missing <head> section (${filePath}).`);
    }
}
function validatePageFragment(html, filePath) {
    const doc = load(html);
    if (doc('main').length === 0) {
        throw new Error(`Page fragment missing <main> section (${filePath}).`);
    }
    if (doc('head').length === 0) {
        throw new Error(`Page fragment missing <head> section (${filePath}).`);
    }
}
function warn(message) {
    console.warn(`[webstir-frontend][html] ${message}`);
}
function dedupeHeadMeta(document, attribute) {
    const head = document('head').first();
    if (head.length === 0) {
        return;
    }
    const seen = new Map();
    head.find(`meta[${attribute}]`).each((_, element) => {
        const value = element.attribs?.[attribute];
        if (!value) {
            return;
        }
        const key = value.toLowerCase();
        const previous = seen.get(key);
        if (previous) {
            previous.remove();
        }
        seen.set(key, document(element));
    });
}
function dedupeHeadLinks(document, attribute) {
    const head = document('head').first();
    if (head.length === 0) {
        return;
    }
    const seen = new Map();
    head.find(`link[${attribute}]`).each((_, element) => {
        const value = element.attribs?.[attribute];
        if (!value) {
            return;
        }
        const key = value.toLowerCase();
        const previous = seen.get(key);
        if (previous) {
            previous.remove();
        }
        seen.set(key, document(element));
    });
}
function removeDevScripts(document) {
    removeDevScript(document, `/${FILES.refreshJs}`);
    removeDevScript(document, `/${FILES.hmrJs}`);
}
function removeDevScript(document, selector) {
    document(`script[src="${selector}"]`).each((_, element) => {
        const script = document(element);
        const next = script.next();
        script.remove();
        if (isWhitespaceTextNode(next)) {
            next.remove();
        }
    });
}
function isWhitespaceTextNode(node) {
    return node.length > 0
        && node[0].type === 'text'
        && (node[0].data ?? '').trim().length === 0;
}
async function minifyHtml(html) {
    return minify(html, {
        collapseWhitespace: true,
        keepClosingSlash: true,
        minifyCSS: false,
        minifyJS: false,
        removeComments: true,
        removeOptionalTags: false,
        removeAttributeQuotes: false
    });
}
async function addImageDimensions(document, context, pageDirectory) {
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
function isExternalSource(src) {
    return src.startsWith('http://')
        || src.startsWith('https://')
        || src.startsWith('data:')
        || src.startsWith('//');
}
function resolveAssetPath(src, pageDirectory, buildRoot) {
    const normalized = src.replace(/\\/g, '/');
    if (normalized.startsWith('/')) {
        const relative = normalized.replace(/^\//, '');
        return path.join(buildRoot, relative);
    }
    return path.join(pageDirectory, normalized);
}
