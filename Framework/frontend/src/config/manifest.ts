import { promises as fs } from 'fs';
import path from 'path';
import { frontendConfigSchema, type FrontendConfigInput } from './schema.js';

export interface WriteManifestOptions {
    readonly outputPath: string;
    readonly data: FrontendConfigInput;
}

export async function writeConfigManifest(options: WriteManifestOptions): Promise<void> {
    const parsed = frontendConfigSchema.parse(options.data);
    const directory = path.dirname(options.outputPath);
    await fs.mkdir(directory, { recursive: true });
    const serialized = JSON.stringify(parsed, undefined, 2);
    const tempPath = path.join(directory, `.webstir-frontend-${process.pid}-${Date.now()}.tmp`);
    await fs.writeFile(tempPath, serialized, 'utf8');
    await fs.rename(tempPath, options.outputPath);
}

export async function readConfigManifest(manifestPath: string): Promise<FrontendConfigInput> {
    const json = await fs.readFile(manifestPath, 'utf8');
    const parsed = JSON.parse(json) as unknown;
    return frontendConfigSchema.parse(parsed);
}
