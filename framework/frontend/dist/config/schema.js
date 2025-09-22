import { z } from 'zod';
export const frontendPathSchema = z.object({
    workspace: z.string(),
    src: z.object({
        root: z.string(),
        frontend: z.string(),
        app: z.string(),
        pages: z.string(),
        images: z.string(),
        fonts: z.string(),
        media: z.string()
    }),
    build: z.object({
        root: z.string(),
        frontend: z.string(),
        app: z.string(),
        pages: z.string(),
        images: z.string(),
        fonts: z.string(),
        media: z.string()
    }),
    dist: z.object({
        root: z.string(),
        frontend: z.string(),
        app: z.string(),
        pages: z.string(),
        images: z.string(),
        fonts: z.string(),
        media: z.string()
    })
});
export const frontendFeatureFlagsSchema = z.object({
    htmlSecurity: z.boolean().default(true),
    imageOptimization: z.boolean().default(true),
    precompression: z.boolean().default(true)
});
export const frontendConfigSchema = z.object({
    version: z.literal(1),
    paths: frontendPathSchema,
    features: frontendFeatureFlagsSchema
});
