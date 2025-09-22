import path from 'node:path';
import { readJson, writeJson, ensureDir } from '../utils/fs.js';
const MANIFEST_FILENAME = 'manifest.json';
export async function updatePageManifest(directory, pageName, updater) {
    const manifestPath = path.join(directory, MANIFEST_FILENAME);
    await ensureDir(directory);
    const manifest = (await readJson(manifestPath)) ?? { pages: {} };
    const pageManifest = manifest.pages[pageName] ?? {};
    updater(pageManifest);
    manifest.pages[pageName] = pageManifest;
    await writeJson(manifestPath, manifest);
}
export async function readPageManifest(directory, pageName) {
    const manifestPath = path.join(directory, MANIFEST_FILENAME);
    const manifest = (await readJson(manifestPath)) ?? { pages: {} };
    return manifest.pages[pageName] ?? {};
}
