export interface FrontendCommandOptions {
    readonly workspaceRoot: string;
    readonly changedFile?: string;
    readonly watch?: boolean;
}

export interface FrontendConfig {
    readonly version: 1;
    readonly paths: FrontendPathConfig;
    readonly features: FrontendFeatureFlags;
    readonly publish: FrontendPublishConfig;
}

export interface FrontendPathConfig {
    readonly workspace: string;
    readonly src: {
        readonly root: string;
        readonly frontend: string;
        readonly app: string;
        readonly pages: string;
        readonly images: string;
        readonly fonts: string;
        readonly media: string;
    };
    readonly build: {
        readonly root: string;
        readonly frontend: string;
        readonly app: string;
        readonly pages: string;
        readonly images: string;
        readonly fonts: string;
        readonly media: string;
    };
    readonly dist: {
        readonly root: string;
        readonly frontend: string;
        readonly app: string;
        readonly pages: string;
        readonly images: string;
        readonly fonts: string;
        readonly media: string;
    };
}

export interface FrontendFeatureFlags {
    readonly htmlSecurity: boolean;
    readonly imageOptimization: boolean;
    readonly precompression: boolean;
}

export interface FrontendPublishConfig {
    readonly basePath: string;
}

export interface AddPageCommandOptions extends FrontendCommandOptions {
    readonly pageName: string;
}
