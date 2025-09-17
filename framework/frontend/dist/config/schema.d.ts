import { z } from 'zod';
export declare const frontendPathSchema: z.ZodObject<{
    workspace: z.ZodString;
    src: z.ZodObject<{
        root: z.ZodString;
        frontend: z.ZodString;
        app: z.ZodString;
        pages: z.ZodString;
        images: z.ZodString;
        fonts: z.ZodString;
        media: z.ZodString;
    }, "strip", z.ZodTypeAny, {
        root: string;
        frontend: string;
        app: string;
        pages: string;
        images: string;
        fonts: string;
        media: string;
    }, {
        root: string;
        frontend: string;
        app: string;
        pages: string;
        images: string;
        fonts: string;
        media: string;
    }>;
    build: z.ZodObject<{
        root: z.ZodString;
        frontend: z.ZodString;
        app: z.ZodString;
        pages: z.ZodString;
        images: z.ZodString;
        fonts: z.ZodString;
        media: z.ZodString;
    }, "strip", z.ZodTypeAny, {
        root: string;
        frontend: string;
        app: string;
        pages: string;
        images: string;
        fonts: string;
        media: string;
    }, {
        root: string;
        frontend: string;
        app: string;
        pages: string;
        images: string;
        fonts: string;
        media: string;
    }>;
    dist: z.ZodObject<{
        root: z.ZodString;
        frontend: z.ZodString;
        app: z.ZodString;
        pages: z.ZodString;
        images: z.ZodString;
        fonts: z.ZodString;
        media: z.ZodString;
    }, "strip", z.ZodTypeAny, {
        root: string;
        frontend: string;
        app: string;
        pages: string;
        images: string;
        fonts: string;
        media: string;
    }, {
        root: string;
        frontend: string;
        app: string;
        pages: string;
        images: string;
        fonts: string;
        media: string;
    }>;
}, "strip", z.ZodTypeAny, {
    workspace: string;
    src: {
        root: string;
        frontend: string;
        app: string;
        pages: string;
        images: string;
        fonts: string;
        media: string;
    };
    build: {
        root: string;
        frontend: string;
        app: string;
        pages: string;
        images: string;
        fonts: string;
        media: string;
    };
    dist: {
        root: string;
        frontend: string;
        app: string;
        pages: string;
        images: string;
        fonts: string;
        media: string;
    };
}, {
    workspace: string;
    src: {
        root: string;
        frontend: string;
        app: string;
        pages: string;
        images: string;
        fonts: string;
        media: string;
    };
    build: {
        root: string;
        frontend: string;
        app: string;
        pages: string;
        images: string;
        fonts: string;
        media: string;
    };
    dist: {
        root: string;
        frontend: string;
        app: string;
        pages: string;
        images: string;
        fonts: string;
        media: string;
    };
}>;
export declare const frontendFeatureFlagsSchema: z.ZodObject<{
    htmlSecurity: z.ZodDefault<z.ZodBoolean>;
    imageOptimization: z.ZodDefault<z.ZodBoolean>;
    precompression: z.ZodDefault<z.ZodBoolean>;
}, "strip", z.ZodTypeAny, {
    htmlSecurity: boolean;
    imageOptimization: boolean;
    precompression: boolean;
}, {
    htmlSecurity?: boolean | undefined;
    imageOptimization?: boolean | undefined;
    precompression?: boolean | undefined;
}>;
export declare const frontendConfigSchema: z.ZodObject<{
    version: z.ZodLiteral<1>;
    paths: z.ZodObject<{
        workspace: z.ZodString;
        src: z.ZodObject<{
            root: z.ZodString;
            frontend: z.ZodString;
            app: z.ZodString;
            pages: z.ZodString;
            images: z.ZodString;
            fonts: z.ZodString;
            media: z.ZodString;
        }, "strip", z.ZodTypeAny, {
            root: string;
            frontend: string;
            app: string;
            pages: string;
            images: string;
            fonts: string;
            media: string;
        }, {
            root: string;
            frontend: string;
            app: string;
            pages: string;
            images: string;
            fonts: string;
            media: string;
        }>;
        build: z.ZodObject<{
            root: z.ZodString;
            frontend: z.ZodString;
            app: z.ZodString;
            pages: z.ZodString;
            images: z.ZodString;
            fonts: z.ZodString;
            media: z.ZodString;
        }, "strip", z.ZodTypeAny, {
            root: string;
            frontend: string;
            app: string;
            pages: string;
            images: string;
            fonts: string;
            media: string;
        }, {
            root: string;
            frontend: string;
            app: string;
            pages: string;
            images: string;
            fonts: string;
            media: string;
        }>;
        dist: z.ZodObject<{
            root: z.ZodString;
            frontend: z.ZodString;
            app: z.ZodString;
            pages: z.ZodString;
            images: z.ZodString;
            fonts: z.ZodString;
            media: z.ZodString;
        }, "strip", z.ZodTypeAny, {
            root: string;
            frontend: string;
            app: string;
            pages: string;
            images: string;
            fonts: string;
            media: string;
        }, {
            root: string;
            frontend: string;
            app: string;
            pages: string;
            images: string;
            fonts: string;
            media: string;
        }>;
    }, "strip", z.ZodTypeAny, {
        workspace: string;
        src: {
            root: string;
            frontend: string;
            app: string;
            pages: string;
            images: string;
            fonts: string;
            media: string;
        };
        build: {
            root: string;
            frontend: string;
            app: string;
            pages: string;
            images: string;
            fonts: string;
            media: string;
        };
        dist: {
            root: string;
            frontend: string;
            app: string;
            pages: string;
            images: string;
            fonts: string;
            media: string;
        };
    }, {
        workspace: string;
        src: {
            root: string;
            frontend: string;
            app: string;
            pages: string;
            images: string;
            fonts: string;
            media: string;
        };
        build: {
            root: string;
            frontend: string;
            app: string;
            pages: string;
            images: string;
            fonts: string;
            media: string;
        };
        dist: {
            root: string;
            frontend: string;
            app: string;
            pages: string;
            images: string;
            fonts: string;
            media: string;
        };
    }>;
    features: z.ZodObject<{
        htmlSecurity: z.ZodDefault<z.ZodBoolean>;
        imageOptimization: z.ZodDefault<z.ZodBoolean>;
        precompression: z.ZodDefault<z.ZodBoolean>;
    }, "strip", z.ZodTypeAny, {
        htmlSecurity: boolean;
        imageOptimization: boolean;
        precompression: boolean;
    }, {
        htmlSecurity?: boolean | undefined;
        imageOptimization?: boolean | undefined;
        precompression?: boolean | undefined;
    }>;
}, "strip", z.ZodTypeAny, {
    version: 1;
    paths: {
        workspace: string;
        src: {
            root: string;
            frontend: string;
            app: string;
            pages: string;
            images: string;
            fonts: string;
            media: string;
        };
        build: {
            root: string;
            frontend: string;
            app: string;
            pages: string;
            images: string;
            fonts: string;
            media: string;
        };
        dist: {
            root: string;
            frontend: string;
            app: string;
            pages: string;
            images: string;
            fonts: string;
            media: string;
        };
    };
    features: {
        htmlSecurity: boolean;
        imageOptimization: boolean;
        precompression: boolean;
    };
}, {
    version: 1;
    paths: {
        workspace: string;
        src: {
            root: string;
            frontend: string;
            app: string;
            pages: string;
            images: string;
            fonts: string;
            media: string;
        };
        build: {
            root: string;
            frontend: string;
            app: string;
            pages: string;
            images: string;
            fonts: string;
            media: string;
        };
        dist: {
            root: string;
            frontend: string;
            app: string;
            pages: string;
            images: string;
            fonts: string;
            media: string;
        };
    };
    features: {
        htmlSecurity?: boolean | undefined;
        imageOptimization?: boolean | undefined;
        precompression?: boolean | undefined;
    };
}>;
export type FrontendConfigInput = z.infer<typeof frontendConfigSchema>;
