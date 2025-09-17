import type { BuilderContext } from '../builders/types.js';
interface Rule {
    readonly directory: string;
    readonly extensions?: readonly string[];
}
export declare function shouldProcess(context: BuilderContext, rules: readonly Rule[]): boolean;
export declare function isPathInside(target: string, directory: string): boolean;
export {};
