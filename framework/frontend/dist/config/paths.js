import path from 'path';
import { promises as fs } from 'fs';
import { FOLDERS } from '../core/constants.js';
export const FRONTEND_MANIFEST_FILENAME = 'frontend-manifest.json';
export function resolveManifestPath(workspaceRoot) {
    return path.join(workspaceRoot, FOLDERS.tools, FRONTEND_MANIFEST_FILENAME);
}
export async function ensureToolsDirectory(workspaceRoot) {
    const toolsPath = path.join(workspaceRoot, FOLDERS.tools);
    await fs.mkdir(toolsPath, { recursive: true });
}
