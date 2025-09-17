export interface PageInfo {
    readonly name: string;
    readonly directory: string;
}
export declare function getPages(root: string): Promise<PageInfo[]>;
export declare function getPageDirectories(root: string): Promise<PageInfo[]>;
