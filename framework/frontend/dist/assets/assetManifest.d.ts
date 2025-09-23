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
export declare function updatePageManifest(directory: string, pageName: string, updater: (value: PageAssetManifest) => void): Promise<void>;
export declare function readPageManifest(directory: string, pageName: string): Promise<PageAssetManifest>;
export declare function updateSharedAssets(directory: string, updater: (value: SharedAssets) => void): Promise<void>;
export declare function readSharedAssets(directory: string): Promise<SharedAssets | null>;
