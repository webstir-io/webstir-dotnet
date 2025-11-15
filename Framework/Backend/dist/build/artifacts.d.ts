import type { ModuleArtifact, ModuleDiagnostic, ModuleManifest } from '@webstir-io/module-contract';
export declare function collectArtifacts(buildRoot: string, includeSourceMaps: boolean): Promise<ModuleArtifact[]>;
export declare function createBuildManifest(buildRoot: string, artifacts: readonly ModuleArtifact[], diagnostics: ModuleDiagnostic[], moduleManifest: ModuleManifest): {
    entryPoints: string[];
    staticAssets: never[];
    diagnostics: ModuleDiagnostic[];
    module: {
        kind: "frontend" | "backend";
        name: string;
        contractVersion: string;
        version: string;
        capabilities?: string[] | undefined;
        assets?: string[] | undefined;
        middlewares?: string[] | undefined;
        routes?: {
            name: string;
            path: string;
            method: "GET" | "POST" | "DELETE" | "PUT" | "PATCH" | "HEAD" | "OPTIONS";
            summary?: string | undefined;
            description?: string | undefined;
            tags?: string[] | undefined;
            errors?: {
                code: "validation" | "auth" | "not_found" | "domain" | "conflict" | "internal";
                message: string;
                details?: unknown;
                cause?: unknown;
                correlationId?: string | undefined;
            }[] | undefined;
            input?: {
                params?: {
                    kind: "zod" | "json-schema" | "ts-rest";
                    name: string;
                    source?: string | undefined;
                } | undefined;
                query?: {
                    kind: "zod" | "json-schema" | "ts-rest";
                    name: string;
                    source?: string | undefined;
                } | undefined;
                body?: {
                    kind: "zod" | "json-schema" | "ts-rest";
                    name: string;
                    source?: string | undefined;
                } | undefined;
                headers?: {
                    kind: "zod" | "json-schema" | "ts-rest";
                    name: string;
                    source?: string | undefined;
                } | undefined;
            } | undefined;
            output?: {
                body: {
                    kind: "zod" | "json-schema" | "ts-rest";
                    name: string;
                    source?: string | undefined;
                };
                status?: number | undefined;
                headers?: {
                    kind: "zod" | "json-schema" | "ts-rest";
                    name: string;
                    source?: string | undefined;
                } | undefined;
            } | undefined;
        }[] | undefined;
        views?: {
            name: string;
            path: string;
            params?: {
                kind: "zod" | "json-schema" | "ts-rest";
                name: string;
                source?: string | undefined;
            } | undefined;
            summary?: string | undefined;
            description?: string | undefined;
            tags?: string[] | undefined;
            data?: {
                kind: "zod" | "json-schema" | "ts-rest";
                name: string;
                source?: string | undefined;
            } | undefined;
        }[] | undefined;
        jobs?: {
            name: string;
            schedule?: string | undefined;
            priority?: string | number | undefined;
        }[] | undefined;
        events?: {
            name: string;
            description?: string | undefined;
            payload?: {
                kind: "zod" | "json-schema" | "ts-rest";
                name: string;
                source?: string | undefined;
            } | undefined;
        }[] | undefined;
        services?: {
            name: string;
            description?: string | undefined;
        }[] | undefined;
        init?: string | undefined;
        dispose?: string | undefined;
    };
};
