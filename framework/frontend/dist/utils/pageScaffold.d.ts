export interface PageScaffoldOptions {
    readonly workspaceRoot: string;
    readonly pageName: string;
    readonly paths: {
        readonly pages: string;
        readonly app: string;
    };
}
export declare function createPageScaffold(options: PageScaffoldOptions): Promise<void>;
