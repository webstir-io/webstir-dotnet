import type { Builder, BuilderContext } from '../builders/types.js';
import { WatchReporter, type SerializedMessage } from './watchReporter.js';
import type { HotAsset, HotUpdateDetails } from './hotUpdateTracker.js';
export interface JavaScriptBuildSummary {
    readonly pagesBuilt: readonly string[];
    readonly warnings: readonly SerializedMessage[];
    readonly modules: readonly HotAsset[];
    readonly requiresReload: boolean;
    readonly fallbackReasons: readonly string[];
}
export interface AdditionalBuildResult {
    readonly succeeded: boolean;
    readonly assets: readonly string[];
    readonly styles: readonly HotAsset[];
    readonly requiresReload: boolean;
    readonly fallbackReasons: readonly string[];
}
export declare function runBuilderWithDiagnostics(builder: Builder, reporter: WatchReporter, context: BuilderContext, changedFile: string | undefined, relativeChange: string | undefined): Promise<boolean>;
export declare function emitPipelineSuccess(summary: JavaScriptBuildSummary, assetsResult: AdditionalBuildResult, changedFile: string | undefined, relativeChange: string | undefined, hotUpdate: HotUpdateDetails): void;
export declare function serializeSummary(summary: JavaScriptBuildSummary, changedFile: string | undefined, skipped: boolean): Record<string, unknown>;
export declare function emitJavaScriptFailure(error: unknown, changedFile?: string): void;
export declare class JavaScriptBuildError extends Error {
    readonly pageName: string;
    readonly details: readonly SerializedMessage[];
    constructor(pageName: string, cause: unknown);
}
