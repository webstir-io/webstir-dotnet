import path from 'node:path';
import { readJson, writeJson, ensureDir } from '../utils/fs.js';

export interface PageAssetManifest {
    js?: string;
    css?: string;
}

export interface AssetManifest {
    pages: Record<string, PageAssetManifest>;
    shared?: SharedAssets;
}

export interface SharedAssets {
    css?: string;
}

const MANIFEST_FILENAME = 'manifest.json';

export async function updatePageManifest(directory: string, pageName: string, updater: (value: PageAssetManifest) => void): Promise<void> {
    const manifestPath = path.join(directory, MANIFEST_FILENAME);
    await ensureDir(directory);
    const manifest = (await readJson<AssetManifest>(manifestPath)) ?? { pages: {} };
    const pageManifest: PageAssetManifest = manifest.pages[pageName] ?? {};
    updater(pageManifest);
    manifest.pages[pageName] = pageManifest;
    await writeJson(manifestPath, manifest);
}

export async function readPageManifest(directory: string, pageName: string): Promise<PageAssetManifest> {
    const manifestPath = path.join(directory, MANIFEST_FILENAME);
    const manifest = (await readJson<AssetManifest>(manifestPath)) ?? { pages: {} };
    return manifest.pages[pageName] ?? {};
}

export async function updateSharedAssets(directory: string, updater: (value: SharedAssets) => void): Promise<void> {
    const manifestPath = path.join(directory, MANIFEST_FILENAME);
    await ensureDir(directory);
    const manifest = (await readJson<AssetManifest>(manifestPath)) ?? { pages: {} };
    const shared: SharedAssets = manifest.shared ?? {};
    updater(shared);
    manifest.shared = shared;
    await writeJson(manifestPath, manifest);
}

export async function readSharedAssets(directory: string): Promise<SharedAssets | null> {
    const manifestPath = path.join(directory, MANIFEST_FILENAME);
    const manifest = await readJson<AssetManifest>(manifestPath);
    return manifest?.shared ?? null;
}
