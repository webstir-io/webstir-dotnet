export interface PageAssetManifest {
    js?: string;
    css?: string;
}
export interface AssetManifest {
    pages: Record<string, PageAssetManifest>;
}
export declare function updatePageManifest(directory: string, pageName: string, updater: (value: PageAssetManifest) => void): Promise<void>;
export declare function readPageManifest(directory: string, pageName: string): Promise<PageAssetManifest>;
