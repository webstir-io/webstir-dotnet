import type { BuildResult } from 'esbuild';
import type { FrontendConfig } from '../types.js';
import type { BuilderContext } from '../builders/types.js';
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
export declare class HotUpdateTracker {
    private readonly workspaceRoot;
    private readonly pageOutputHashes;
    private readonly assetFingerprints;
    constructor(options: HotUpdateTrackerOptions);
    reset(): void;
    removePage(pageName: string): void;
    processJavaScriptResult(pageName: string, result: BuildResult, config: FrontendConfig): Promise<ProcessJavaScriptResult>;
    collectCssChanges(context: BuilderContext, pageNames: readonly string[]): Promise<CollectCssResult>;
    private computeAssetFingerprint;
    private getPageCssOutputPath;
    private getAppCssOutputPath;
    private createHotAsset;
    private resolveOutputPath;
    private toWebPath;
}
export {};
