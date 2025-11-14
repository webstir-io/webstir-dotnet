import type { ModuleManifest } from '@webstir-io/module-contract';

type ModuleRoute = NonNullable<ModuleManifest['routes']>[number];

export interface BackendTestContext {
    readonly baseUrl: string;
    readonly url: URL;
    readonly port: number;
    readonly manifest: ModuleManifest | null;
    readonly routes: readonly ModuleRoute[];
    readonly env: Readonly<Record<string, string>>;
    request(pathOrUrl?: string | URL, init?: RequestInit): Promise<Response>;
}

export interface BackendTestHarness {
    readonly context: BackendTestContext;
    stop(): Promise<void>;
}

export interface BackendTestHarnessOptions {
    workspaceRoot?: string;
    buildRoot?: string;
    entry?: string;
    manifestPath?: string;
    env?: Record<string, string | undefined>;
    port?: number;
    readyText?: string;
    readyTimeoutMs?: number;
    reuseExistingServer?: boolean;
}

export type BackendTestCallback = (context: BackendTestContext) => Promise<void> | void;
