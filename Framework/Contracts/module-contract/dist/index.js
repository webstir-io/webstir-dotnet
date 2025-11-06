import { z } from 'zod';
// Centralized manifest contract version used by providers and examples.
export const CONTRACT_VERSION = '1.0.0';
export const contractVersionLiteral = z.literal(CONTRACT_VERSION);
export const moduleKindSchema = z.enum(['frontend', 'backend']);
export const logLevelSchema = z.enum(['trace', 'debug', 'info', 'warn', 'error', 'fatal']);
export const moduleErrorCodeSchema = z.enum(['validation', 'auth', 'not_found', 'domain', 'conflict', 'internal']);
export const moduleErrorSchema = z.object({
    code: moduleErrorCodeSchema,
    message: z.string(),
    details: z.unknown().optional(),
    cause: z.unknown().optional(),
    correlationId: z.string().optional()
});
export const httpMethodSchema = z.enum(['GET', 'HEAD', 'POST', 'PUT', 'PATCH', 'DELETE', 'OPTIONS']);
export const schemaReferenceSchema = z.object({
    kind: z.enum(['zod', 'json-schema', 'ts-rest']).default('zod'),
    name: z.string(),
    source: z.string().optional()
});
export const routeInputSchema = z
    .object({
    params: schemaReferenceSchema.optional(),
    query: schemaReferenceSchema.optional(),
    body: schemaReferenceSchema.optional(),
    headers: schemaReferenceSchema.optional()
})
    .partial()
    .strict();
export const routeOutputSchema = z.object({
    body: schemaReferenceSchema,
    status: z.number().int().min(100).max(599).optional(),
    headers: schemaReferenceSchema.optional()
});
export const routeDefinitionSchema = z.object({
    name: z.string().min(1),
    method: httpMethodSchema,
    path: z.string().min(1),
    summary: z.string().optional(),
    description: z.string().optional(),
    tags: z.array(z.string()).optional(),
    input: routeInputSchema.optional(),
    output: routeOutputSchema.optional(),
    errors: z.array(moduleErrorSchema).optional()
});
export const viewDefinitionSchema = z.object({
    name: z.string().min(1),
    path: z.string().min(1),
    summary: z.string().optional(),
    description: z.string().optional(),
    tags: z.array(z.string()).optional(),
    params: schemaReferenceSchema.optional(),
    data: schemaReferenceSchema.optional()
});
export const jobDefinitionSchema = z.object({
    name: z.string().min(1),
    schedule: z.string().optional(),
    priority: z.union([z.number().int(), z.string()]).optional()
});
export const eventDefinitionSchema = z.object({
    name: z.string().min(1),
    payload: schemaReferenceSchema.optional(),
    description: z.string().optional()
});
export const serviceDefinitionSchema = z.object({
    name: z.string().min(1),
    description: z.string().optional()
});
export const moduleManifestSchema = z.object({
    contractVersion: z.string().min(1),
    name: z.string().min(1),
    version: z.string().min(1),
    kind: moduleKindSchema,
    capabilities: z.array(z.string()).optional(),
    // Optional pass-through metadata for providers
    assets: z.array(z.string()).optional(),
    middlewares: z.array(z.string()).optional(),
    routes: z.array(routeDefinitionSchema).optional(),
    views: z.array(viewDefinitionSchema).optional(),
    jobs: z.array(jobDefinitionSchema).optional(),
    events: z.array(eventDefinitionSchema).optional(),
    services: z.array(serviceDefinitionSchema).optional(),
    init: z.string().optional(),
    dispose: z.string().optional()
});
export function defineRoute(spec) {
    return spec;
}
export function defineView(spec) {
    return spec;
}
export function createModule(definition) {
    return definition;
}
export { fromTsRestRoute, fromTsRestRouter } from './adapters/ts-rest.js';
