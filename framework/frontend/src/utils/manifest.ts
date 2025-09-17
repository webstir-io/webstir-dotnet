import path from 'path';
import { promises as fs } from 'fs';
import { FOLDERS } from './constants.js';

export const FRONTEND_MANIFEST_FILENAME = 'frontend-manifest.json';

export function resolveManifestPath(workspaceRoot: string): string {
    return path.join(workspaceRoot, FOLDERS.tools, FRONTEND_MANIFEST_FILENAME);
}

export async function ensureToolsDirectory(workspaceRoot: string): Promise<void> {
    const toolsPath = path.join(workspaceRoot, FOLDERS.tools);
    await fs.mkdir(toolsPath, { recursive: true });
}
