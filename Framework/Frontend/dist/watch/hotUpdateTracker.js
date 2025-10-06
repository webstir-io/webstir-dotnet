import path from 'node:path';
import { createHash } from 'node:crypto';
import { readFile } from 'node:fs/promises';
import { FOLDERS, FILES, EXTENSIONS } from '../core/constants.js';
import { emitDiagnostic } from '../core/diagnostics.js';
import { pathExists } from '../utils/fs.js';
import { isPathInside } from '../utils/changedFile.js';
import { findPageFromChangedFile } from '../utils/pathMatch.js';
export class HotUpdateTracker {
    workspaceRoot;
    pageOutputHashes = new Map();
    assetFingerprints = new Map();
    constructor(options) {
        this.workspaceRoot = options.workspaceRoot;
    }
    reset() {
        this.pageOutputHashes.clear();
        this.assetFingerprints.clear();
    }
    removePage(pageName) {
        this.pageOutputHashes.delete(pageName);
    }
    async processJavaScriptResult(pageName, result, config) {
        const modules = [];
        let requiresReload = false;
        const fallbackReasons = [];
        const metafile = result.metafile;
        if (!metafile) {
            fallbackReasons.push('javascript.metafile.missing');
            return { modules, requiresReload: true, fallbackReasons };
        }
        const buildRoot = config.paths.build.frontend;
        const currentOutputs = new Set();
        const previousOutputs = this.pageOutputHashes.get(pageName) ?? new Map();
        for (const outputPath of Object.keys(metafile.outputs)) {
            const extension = path.extname(outputPath).toLowerCase();
            if (extension !== EXTENSIONS.js && extension !== '.mjs') {
                continue;
            }
            const absoluteOutput = this.resolveOutputPath(outputPath);
            currentOutputs.add(absoluteOutput);
            const fingerprint = await this.computeAssetFingerprint(absoluteOutput, buildRoot, 'js');
            if (!fingerprint) {
                if (this.assetFingerprints.has(absoluteOutput)) {
                    this.assetFingerprints.delete(absoluteOutput);
                }
                if (previousOutputs.has(absoluteOutput)) {
                    previousOutputs.delete(absoluteOutput);
                    requiresReload = true;
                    fallbackReasons.push('javascript.output.missing');
                }
                continue;
            }
            if (fingerprint.requiresReload) {
                requiresReload = true;
                fallbackReasons.push('javascript.fingerprint.error');
            }
            if (fingerprint.changed) {
                modules.push(fingerprint.asset);
            }
            if (fingerprint.hash) {
                previousOutputs.set(absoluteOutput, { hash: fingerprint.hash });
            }
            else if (previousOutputs.has(absoluteOutput)) {
                previousOutputs.delete(absoluteOutput);
            }
        }
        for (const known of Array.from(previousOutputs.keys())) {
            if (!currentOutputs.has(known)) {
                previousOutputs.delete(known);
                requiresReload = true;
                fallbackReasons.push('javascript.output.removed');
            }
        }
        this.pageOutputHashes.set(pageName, previousOutputs);
        return { modules, requiresReload, fallbackReasons: uniqueReasons(fallbackReasons) };
    }
    async collectCssChanges(context, pageNames) {
        const { config, changedFile } = context;
        const buildRoot = config.paths.build.frontend;
        const candidates = new Set();
        if (!changedFile) {
            for (const page of pageNames) {
                candidates.add(this.getPageCssOutputPath(config, page));
            }
            candidates.add(this.getAppCssOutputPath(config));
        }
        else {
            const normalized = path.resolve(changedFile);
            const extension = path.extname(normalized).toLowerCase();
            if (extension === EXTENSIONS.css) {
                if (isPathInside(normalized, config.paths.src.app)) {
                    for (const page of pageNames) {
                        candidates.add(this.getPageCssOutputPath(config, page));
                    }
                    candidates.add(this.getAppCssOutputPath(config));
                }
                else if (isPathInside(normalized, config.paths.src.pages)) {
                    const page = findPageFromChangedFile(normalized, config.paths.src.pages);
                    if (page) {
                        candidates.add(this.getPageCssOutputPath(config, page));
                    }
                }
                else if (isPathInside(normalized, config.paths.src.frontend)) {
                    for (const page of pageNames) {
                        candidates.add(this.getPageCssOutputPath(config, page));
                    }
                    candidates.add(this.getAppCssOutputPath(config));
                }
            }
        }
        if (candidates.size === 0 && !changedFile) {
            candidates.add(this.getAppCssOutputPath(config));
            for (const page of pageNames) {
                candidates.add(this.getPageCssOutputPath(config, page));
            }
        }
        const styles = [];
        let requiresReload = !changedFile;
        const fallbackReasons = [];
        if (!changedFile) {
            fallbackReasons.push('css.full-rebuild');
        }
        for (const candidate of candidates) {
            const fingerprint = await this.computeAssetFingerprint(candidate, buildRoot, 'css');
            if (!fingerprint) {
                if (this.assetFingerprints.has(path.resolve(candidate))) {
                    this.assetFingerprints.delete(path.resolve(candidate));
                    requiresReload = true;
                    fallbackReasons.push('css.asset.missing');
                }
                continue;
            }
            if (fingerprint.requiresReload) {
                requiresReload = true;
                fallbackReasons.push('css.fingerprint.error');
            }
            if (fingerprint.changed) {
                styles.push(fingerprint.asset);
            }
        }
        return { styles, requiresReload, fallbackReasons: uniqueReasons(fallbackReasons) };
    }
    async computeAssetFingerprint(filePath, buildRoot, type) {
        const absolutePath = path.resolve(filePath);
        if (!(await pathExists(absolutePath))) {
            return null;
        }
        try {
            const contents = await readFile(absolutePath);
            const hash = createHash('sha1').update(contents).digest('hex');
            const previous = this.assetFingerprints.get(absolutePath);
            const changed = previous !== hash;
            this.assetFingerprints.set(absolutePath, hash);
            return {
                asset: this.createHotAsset(absolutePath, buildRoot, type),
                changed,
                requiresReload: false,
                hash
            };
        }
        catch (error) {
            emitDiagnostic({
                code: 'frontend.watch.unexpected',
                kind: 'watch-daemon',
                stage: 'css-fingerprint',
                severity: 'error',
                message: `Failed to fingerprint asset '${absolutePath}': ${error instanceof Error ? error.message : String(error)}`
            });
            return {
                asset: this.createHotAsset(absolutePath, buildRoot, type),
                changed: false,
                requiresReload: true
            };
        }
    }
    getPageCssOutputPath(config, pageName) {
        return path.join(config.paths.build.frontend, FOLDERS.pages, pageName, `${FILES.index}${EXTENSIONS.css}`);
    }
    getAppCssOutputPath(config) {
        return path.join(config.paths.build.frontend, FOLDERS.app, 'app.css');
    }
    createHotAsset(filePath, buildRoot, type) {
        const relativePath = path.relative(buildRoot, filePath);
        const webPath = this.toWebPath(relativePath);
        return {
            type,
            path: filePath,
            relativePath,
            url: webPath.startsWith('/') ? webPath : `/${webPath}`
        };
    }
    resolveOutputPath(outputPath) {
        if (path.isAbsolute(outputPath)) {
            return outputPath;
        }
        return path.resolve(this.workspaceRoot, outputPath);
    }
    toWebPath(relativePath) {
        return relativePath.split(path.sep).join('/') || '';
    }
}
function uniqueReasons(reasons) {
    return Array.from(new Set(reasons.filter(Boolean)));
}
