import type { FrontendConfig } from '../types.js';
export interface BuilderContext {
    readonly config: FrontendConfig;
    readonly changedFile?: string;
}
export interface Builder {
    readonly name: string;
    build(context: BuilderContext): Promise<void>;
    publish(context: BuilderContext): Promise<void>;
}
export type BuilderFactory = (context: BuilderContext) => Builder;
