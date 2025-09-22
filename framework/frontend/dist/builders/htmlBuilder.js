"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.createHtmlBuilder = createHtmlBuilder;
const node_path_1 = __importDefault(require("node:path"));
const cheerio_1 = require("cheerio");
const glob_1 = require("glob");
const constants_js_1 = require("../core/constants.js");
const fs_js_1 = require("../utils/fs.js");
const pages_js_1 = require("../core/pages.js");
const assetManifest_js_1 = require("../assets/assetManifest.js");
const precompression_js_1 = require("../assets/precompression.js");
const changedFile_js_1 = require("../utils/changedFile.js");
const imageOptimizer_js_1 = require("../assets/imageOptimizer.js");
const lazyLoad_js_1 = require("../html/lazyLoad.js");
const htmlSecurity_js_1 = require("../html/htmlSecurity.js");
const resourceHints_js_1 = require("../html/resourceHints.js");
const criticalCss_js_1 = require("../html/criticalCss.js");
const pathMatch_js_1 = require("../utils/pathMatch.js");
const diagnostics_js_1 = require("../core/diagnostics.js");
function createHtmlBuilder(context) {
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
    if (!(0, changedFile_js_1.shouldProcess)(context, [
        { directory: config.paths.src.pages, extensions: [constants_js_1.EXTENSIONS.html] },
        { directory: config.paths.src.app, extensions: [constants_js_1.EXTENSIONS.html] }
    ])) {
        return;
    }
    const appTemplatePath = node_path_1.default.join(config.paths.src.app, constants_js_1.FILE_NAMES.htmlAppTemplate);
    if (!(await (0, fs_js_1.pathExists)(appTemplatePath))) {
        throw new Error(`Missing base application template: ${appTemplatePath}`);
    }
    const templateHtml = await (0, fs_js_1.readFile)(appTemplatePath);
    validateAppTemplate(templateHtml, appTemplatePath);
    const targetPage = (0, pathMatch_js_1.findPageFromChangedFile)(context.changedFile, config.paths.src.pages);
    const pages = await (0, pages_js_1.getPageDirectories)(config.paths.src.pages);
    await (0, fs_js_1.ensureDir)(config.paths.build.frontend);
    for (const page of pages) {
        if (targetPage && page.name !== targetPage) {
            continue;
        }
        const pageHtmlFiles = await (0, glob_1.glob)('**/*.html', {
            cwd: page.directory,
            nodir: true
        });
        if (pageHtmlFiles.length === 0) {
            warn(`No HTML fragments found for page '${page.name}'.`);
            continue;
        }
        const targetDir = node_path_1.default.join(config.paths.build.frontend, constants_js_1.FOLDERS.pages, page.name);
        await (0, fs_js_1.ensureDir)(targetDir);
        for (const relativeHtml of pageHtmlFiles) {
            const sourceHtmlPath = node_path_1.default.join(page.directory, relativeHtml);
            const fragment = await (0, fs_js_1.readFile)(sourceHtmlPath);
            validatePageFragment(fragment, sourceHtmlPath);
            const mergedHtml = mergeTemplates(templateHtml, fragment);
            const targetPath = node_path_1.default.join(targetDir, node_path_1.default.basename(relativeHtml));
            await (0, fs_js_1.writeFile)(targetPath, mergedHtml);
        }
    }
    // Copy the app template for reference in the build output.
    const buildAppDir = node_path_1.default.join(config.paths.build.frontend, constants_js_1.FOLDERS.app);
    await (0, fs_js_1.ensureDir)(buildAppDir);
    await (0, fs_js_1.writeFile)(node_path_1.default.join(buildAppDir, constants_js_1.FILE_NAMES.htmlAppTemplate), templateHtml);
}
async function publishHtml(context) {
    const { config } = context;
    const buildPagesRoot = node_path_1.default.join(config.paths.build.frontend, constants_js_1.FOLDERS.pages);
    if (!(await (0, fs_js_1.pathExists)(buildPagesRoot))) {
        warn('Skipping HTML publish because no build artifacts were found. Run build first.');
        return;
    }
    const targetPage = (0, pathMatch_js_1.findPageFromChangedFile)(context.changedFile, config.paths.src.pages);
    const pages = await (0, pages_js_1.getPageDirectories)(buildPagesRoot);
    for (const page of pages) {
        if (targetPage && page.name !== targetPage) {
            continue;
        }
        const distDir = node_path_1.default.join(config.paths.dist.frontend, constants_js_1.FOLDERS.pages, page.name);
        await (0, fs_js_1.ensureDir)(distDir);
        const htmlFiles = await (0, glob_1.glob)('**/*.html', {
            cwd: page.directory,
            nodir: true
        });
        const manifest = await (0, assetManifest_js_1.readPageManifest)(distDir, page.name);
        for (const relativeHtml of htmlFiles) {
            const sourcePath = node_path_1.default.join(page.directory, relativeHtml);
            const html = await (0, fs_js_1.readFile)(sourcePath);
            const rewritten = await rewriteForPublish(context, html, page.name, manifest, page.directory);
            const outputPath = node_path_1.default.join(distDir, node_path_1.default.basename(relativeHtml));
            await (0, fs_js_1.writeFile)(outputPath, rewritten);
            await handlePrecompression(context, outputPath);
        }
    }
}
function mergeTemplates(appHtml, pageHtml) {
    const app = (0, cheerio_1.load)(appHtml);
    const page = (0, cheerio_1.load)(pageHtml);
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
async function rewriteForPublish(context, html, pageName, manifest, pageDirectory) {
    const document = (0, cheerio_1.load)(html);
    document(`script[src="/${constants_js_1.FILES.refreshJs}"]`).remove();
    if (manifest.js) {
        const selector = `script[src="${constants_js_1.FILES.index}${constants_js_1.EXTENSIONS.js}"]`;
        document(selector).attr('src', `/${constants_js_1.FOLDERS.pages}/${pageName}/${manifest.js}`);
        document(selector).attr('type', 'module');
    }
    if (manifest.css) {
        const selector = `link[href="${constants_js_1.FILES.index}${constants_js_1.EXTENSIONS.css}"]`;
        document(selector).attr('href', `/${constants_js_1.FOLDERS.pages}/${pageName}/${manifest.css}`);
    }
    (0, lazyLoad_js_1.applyLazyLoading)(document);
    if (context.config.features.imageOptimization) {
        await addImageDimensions(document, context, pageDirectory);
    }
    if (context.config.features.htmlSecurity) {
        await (0, criticalCss_js_1.inlineCriticalCss)(document, pageName, context.config.paths.dist.frontend, manifest.css);
        const sriResult = await (0, htmlSecurity_js_1.addSubresourceIntegrity)(document);
        if (sriResult.failures.length > 0) {
            const resources = sriResult.failures;
            const message = resources.length === 1
                ? `Failed to compute subresource integrity for ${resources[0]}.`
                : `Failed to compute subresource integrity for ${resources.length} resources.`;
            (0, diagnostics_js_1.emitDiagnostic)({
                code: 'frontend.sri.unresolved',
                kind: 'sri',
                stage: 'html.publish',
                severity: 'warning',
                message,
                data: { resources },
                suggestion: 'Verify the resource is reachable and not blocked by auth or network constraints.'
            });
        }
        const hints = (0, resourceHints_js_1.injectResourceHints)(document, pageName);
        if (hints.missingHead) {
            (0, diagnostics_js_1.emitDiagnostic)({
                code: 'frontend.resourceHints.missingHead',
                kind: 'resource-hints',
                stage: 'html.publish',
                severity: 'warning',
                message: 'Unable to inject resource hints because <head> is missing.',
                data: { candidates: hints.candidates }
            });
        }
    }
    return document.root().html() ?? '';
}
async function handlePrecompression(context, outputPath) {
    if (context.config.features.precompression) {
        await (0, precompression_js_1.createCompressedVariants)(outputPath);
        return;
    }
    await Promise.all([
        (0, fs_js_1.remove)(`${outputPath}${constants_js_1.EXTENSIONS.br}`).catch(() => undefined),
        (0, fs_js_1.remove)(`${outputPath}${constants_js_1.EXTENSIONS.gz}`).catch(() => undefined)
    ]);
}
function validateAppTemplate(html, filePath) {
    const doc = (0, cheerio_1.load)(html);
    if (doc('main').length === 0) {
        throw new Error(`Base template missing <main> container (${filePath}).`);
    }
    if (doc('head').length === 0) {
        throw new Error(`Base template missing <head> section (${filePath}).`);
    }
}
function validatePageFragment(html, filePath) {
    const doc = (0, cheerio_1.load)(html);
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
        if (!assetPath || !(await (0, fs_js_1.pathExists)(assetPath))) {
            return;
        }
        const dimensions = await (0, imageOptimizer_js_1.getImageDimensions)(assetPath);
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
        return node_path_1.default.join(buildRoot, relative);
    }
    return node_path_1.default.join(pageDirectory, normalized);
}
