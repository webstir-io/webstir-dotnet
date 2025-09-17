import { type FrontendConfigInput } from './schema.js';
export interface WriteManifestOptions {
    readonly outputPath: string;
    readonly data: FrontendConfigInput;
}
export declare function writeConfigManifest(options: WriteManifestOptions): Promise<void>;
export declare function readConfigManifest(manifestPath: string): Promise<FrontendConfigInput>;
