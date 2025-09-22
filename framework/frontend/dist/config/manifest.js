import { promises as fs } from 'fs';
import path from 'path';
import { frontendConfigSchema } from './schema.js';
export async function writeConfigManifest(options) {
    const parsed = frontendConfigSchema.parse(options.data);
    const directory = path.dirname(options.outputPath);
    await fs.mkdir(directory, { recursive: true });
    const serialized = JSON.stringify(parsed, undefined, 2);
    const tempPath = path.join(directory, `.webstir-frontend-${process.pid}-${Date.now()}.tmp`);
    await fs.writeFile(tempPath, serialized, 'utf8');
    await fs.rename(tempPath, options.outputPath);
}
export async function readConfigManifest(manifestPath) {
    const json = await fs.readFile(manifestPath, 'utf8');
    const parsed = JSON.parse(json);
    return frontendConfigSchema.parse(parsed);
}
