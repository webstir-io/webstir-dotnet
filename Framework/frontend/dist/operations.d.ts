import type { AddPageCommandOptions, FrontendCommandOptions } from './types.js';
export declare function runBuild(options: FrontendCommandOptions): Promise<void>;
export declare function runPublish(options: FrontendCommandOptions): Promise<void>;
export declare function runRebuild(options: FrontendCommandOptions): Promise<void>;
export declare function runAddPage(options: AddPageCommandOptions): Promise<void>;
