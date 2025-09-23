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
export async function updateSharedAssets(directory, updater) {
    const manifestPath = path.join(directory, MANIFEST_FILENAME);
    await ensureDir(directory);
    const manifest = (await readJson(manifestPath)) ?? { pages: {} };
    const shared = manifest.shared ?? {};
    updater(shared);
    manifest.shared = shared;
    await writeJson(manifestPath, manifest);
}
export async function readSharedAssets(directory) {
    const manifestPath = path.join(directory, MANIFEST_FILENAME);
    const manifest = await readJson(manifestPath);
    return manifest?.shared ?? null;
}
