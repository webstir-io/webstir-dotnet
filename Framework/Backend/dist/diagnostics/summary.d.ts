import type { ModuleDiagnostic } from '@webstir-io/module-contract';
export declare function pushEntryBucketSummary(diagnostics: ModuleDiagnostic[], entryPoints: readonly string[]): void;
type Severity = 'info' | 'warn' | 'error';
export declare function normalizeLogLevel(value: unknown): Severity;
export declare function filterDiagnostics(list: readonly ModuleDiagnostic[], min: Severity): readonly ModuleDiagnostic[];
export {};
