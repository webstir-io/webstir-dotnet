import path from 'node:path';
import { createHash } from 'node:crypto';
import { readFile } from 'node:fs/promises';
import type { BuildResult, Metafile } from 'esbuild';
import { FOLDERS, FILES, EXTENSIONS } from '../core/constants.js';
import { emitDiagnostic } from '../core/diagnostics.js';
import type { FrontendConfig } from '../types.js';
import type { BuilderContext } from '../builders/types.js';
import { pathExists } from '../utils/fs.js';
import { isPathInside } from '../utils/changedFile.js';
import { findPageFromChangedFile } from '../utils/pathMatch.js';

export interface HotAsset {
    readonly type: 'js' | 'css';
    readonly path: string;
    readonly relativePath: string;
    readonly url: string;
}

export interface HotUpdateDetails {
    readonly modules: readonly HotAsset[];
    readonly styles: readonly HotAsset[];
    readonly requiresReload: boolean;
    readonly fallbackReasons: readonly string[];
    readonly changedFile?: string;
    readonly stats?: HotUpdateStats;
}

export interface HotUpdateStats {
    readonly hotUpdates: number;
    readonly reloadFallbacks: number;
}

interface ProcessJavaScriptResult {
    readonly modules: readonly HotAsset[];
    readonly requiresReload: boolean;
    readonly fallbackReasons: readonly string[];
}

interface CollectCssResult {
    readonly styles: readonly HotAsset[];
    readonly requiresReload: boolean;
    readonly fallbackReasons: readonly string[];
}

interface HotUpdateTrackerOptions {
    readonly workspaceRoot: string;
}

export class HotUpdateTracker {
    private readonly workspaceRoot: string;
    private readonly pageOutputHashes = new Map<string, Map<string, { hash: string }>>();
    private readonly assetFingerprints = new Map<string, string>();

    public constructor(options: HotUpdateTrackerOptions) {
        this.workspaceRoot = options.workspaceRoot;
    }

    public reset(): void {
        this.pageOutputHashes.clear();
        this.assetFingerprints.clear();
    }

    public removePage(pageName: string): void {
        this.pageOutputHashes.delete(pageName);
    }

    public async processJavaScriptResult(
        pageName: string,
        result: BuildResult,
        config: FrontendConfig
    ): Promise<ProcessJavaScriptResult> {
        const modules: HotAsset[] = [];
        let requiresReload = false;
        const fallbackReasons: string[] = [];
        const metafile = result.metafile as Metafile | undefined;

        if (!metafile) {
            fallbackReasons.push('javascript.metafile.missing');
            return { modules, requiresReload: true, fallbackReasons };
        }

        const buildRoot = config.paths.build.frontend;
        const currentOutputs = new Set<string>();
        const previousOutputs = this.pageOutputHashes.get(pageName) ?? new Map<string, { hash: string }>();

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
            } else if (previousOutputs.has(absoluteOutput)) {
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

    public async collectCssChanges(
        context: BuilderContext,
        pageNames: readonly string[]
    ): Promise<CollectCssResult> {
        const { config, changedFile } = context;
        const buildRoot = config.paths.build.frontend;
        const candidates = new Set<string>();

        if (!changedFile) {
            for (const page of pageNames) {
                candidates.add(this.getPageCssOutputPath(config, page));
            }
            candidates.add(this.getAppCssOutputPath(config));
        } else {
            const normalized = path.resolve(changedFile);
            const extension = path.extname(normalized).toLowerCase();
            if (extension === EXTENSIONS.css) {
                if (isPathInside(normalized, config.paths.src.app)) {
                    for (const page of pageNames) {
                        candidates.add(this.getPageCssOutputPath(config, page));
                    }
                    candidates.add(this.getAppCssOutputPath(config));
                } else if (isPathInside(normalized, config.paths.src.pages)) {
                    const page = findPageFromChangedFile(normalized, config.paths.src.pages);
                    if (page) {
                        candidates.add(this.getPageCssOutputPath(config, page));
                    }
                } else if (isPathInside(normalized, config.paths.src.frontend)) {
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

        const styles: HotAsset[] = [];
        let requiresReload = !changedFile;
        const fallbackReasons: string[] = [];

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

    private async computeAssetFingerprint(
        filePath: string,
        buildRoot: string,
        type: HotAsset['type']
    ): Promise<{ asset: HotAsset; changed: boolean; requiresReload: boolean; hash?: string } | null> {
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
        } catch (error) {
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

    private getPageCssOutputPath(config: FrontendConfig, pageName: string): string {
        return path.join(config.paths.build.frontend, FOLDERS.pages, pageName, `${FILES.index}${EXTENSIONS.css}`);
    }

    private getAppCssOutputPath(config: FrontendConfig): string {
        return path.join(config.paths.build.frontend, FOLDERS.app, 'app.css');
    }

    private createHotAsset(filePath: string, buildRoot: string, type: HotAsset['type']): HotAsset {
        const relativePath = path.relative(buildRoot, filePath);
        const webPath = this.toWebPath(relativePath);
        return {
            type,
            path: filePath,
            relativePath,
            url: webPath.startsWith('/') ? webPath : `/${webPath}`
        };
    }

    private resolveOutputPath(outputPath: string): string {
        if (path.isAbsolute(outputPath)) {
            return outputPath;
        }

        return path.resolve(this.workspaceRoot, outputPath);
    }

    private toWebPath(relativePath: string): string {
        return relativePath.split(path.sep).join('/') || '';
    }
}

function uniqueReasons(reasons: readonly string[]): readonly string[] {
    return Array.from(new Set(reasons.filter(Boolean)));
}
