import type { ModuleDiagnostic } from '@webstir-io/module-contract';

export function pushEntryBucketSummary(diagnostics: ModuleDiagnostic[], entryPoints: readonly string[]): void {
    try {
        const server = entryPoints.filter(
            (p) => p === 'index.js' || /(^|\/)index\.js$/.test(p) && !/^(functions|jobs)\//.test(p)
        ).length;
        const functionsCount = entryPoints.filter((p) => p.startsWith('functions/')).length;
        const jobsCount = entryPoints.filter((p) => p.startsWith('jobs/')).length;
        diagnostics.push({
            severity: 'info',
            message: `[webstir-backend] entries by bucket: server=${server} functions=${functionsCount} jobs=${jobsCount}`
        });
    } catch {
        // best-effort only
    }
}

type Severity = 'info' | 'warn' | 'error';

export function normalizeLogLevel(value: unknown): Severity {
    if (typeof value !== 'string') return 'info';
    const v = value.toLowerCase();
    if (v === 'error' || v === 'warn' || v === 'info') return v;
    return 'info';
}

export function filterDiagnostics(list: readonly ModuleDiagnostic[], min: Severity): readonly ModuleDiagnostic[] {
    const rank = (s: Severity) => (s === 'error' ? 3 : s === 'warn' ? 2 : 1);
    const threshold = rank(min);
    return list.filter((d) => rank(d.severity as Severity) >= threshold);
}
