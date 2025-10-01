import path from 'path';
import { promises as fs } from 'fs';
import { FOLDERS } from '../core/constants.js';

export const FRONTEND_MANIFEST_FILENAME = 'frontend-manifest.json';

export function resolveManifestPath(workspaceRoot: string): string {
    return path.join(workspaceRoot, FOLDERS.webstir, FRONTEND_MANIFEST_FILENAME);
}

export async function ensureWebstirDirectory(workspaceRoot: string): Promise<void> {
    const webstirPath = path.join(workspaceRoot, FOLDERS.webstir);
    await fs.mkdir(webstirPath, { recursive: true });
}
