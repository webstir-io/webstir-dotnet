"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.frontendConfigSchema = exports.frontendFeatureFlagsSchema = exports.frontendPathSchema = void 0;
const zod_1 = require("zod");
exports.frontendPathSchema = zod_1.z.object({
    workspace: zod_1.z.string(),
    src: zod_1.z.object({
        root: zod_1.z.string(),
        frontend: zod_1.z.string(),
        app: zod_1.z.string(),
        pages: zod_1.z.string(),
        images: zod_1.z.string(),
        fonts: zod_1.z.string(),
        media: zod_1.z.string()
    }),
    build: zod_1.z.object({
        root: zod_1.z.string(),
        frontend: zod_1.z.string(),
        app: zod_1.z.string(),
        pages: zod_1.z.string(),
        images: zod_1.z.string(),
        fonts: zod_1.z.string(),
        media: zod_1.z.string()
    }),
    dist: zod_1.z.object({
        root: zod_1.z.string(),
        frontend: zod_1.z.string(),
        app: zod_1.z.string(),
        pages: zod_1.z.string(),
        images: zod_1.z.string(),
        fonts: zod_1.z.string(),
        media: zod_1.z.string()
    })
});
exports.frontendFeatureFlagsSchema = zod_1.z.object({
    htmlSecurity: zod_1.z.boolean().default(true),
    imageOptimization: zod_1.z.boolean().default(true),
    precompression: zod_1.z.boolean().default(true)
});
exports.frontendConfigSchema = zod_1.z.object({
    version: zod_1.z.literal(1),
    paths: exports.frontendPathSchema,
    features: exports.frontendFeatureFlagsSchema
});
