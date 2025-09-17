import path from 'node:path';
import postcss from 'postcss';
import autoprefixer from 'autoprefixer';
import csso from 'csso';
import { FOLDERS, FILES, EXTENSIONS } from '../utils/constants.js';
import { ensureDir, pathExists, readFile, writeFile } from '../utils/fs.js';
import type { Builder, BuilderContext } from './types.js';
import { getPages } from '../utils/pages.js';
import { hashContent } from '../utils/hash.js';
import { updatePageManifest } from '../utils/assetManifest.js';
import { createCompressedVariants } from '../utils/precompression.js';
import { shouldProcess } from '../utils/changedFile.js';
import { findPageFromChangedFile } from '../utils/pathMatch.js';

const MODULE_SUFFIX = '.module';

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

        if (isProduction) {
            await emitProductionCss(config, page.name, processed.css);
        } else {
            await emitDevelopmentCss(config, page.name, processed.css);
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
    await createCompressedVariants(outputPath);
    await updatePageManifest(outputDir, pageName, (manifest) => {
        manifest.css = fileName;
    });
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
