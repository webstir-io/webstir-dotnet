export function pushEntryBucketSummary(diagnostics, entryPoints) {
    try {
        const server = entryPoints.filter((p) => p === 'index.js' || /(^|\/)index\.js$/.test(p) && !/^(functions|jobs)\//.test(p)).length;
        const functionsCount = entryPoints.filter((p) => p.startsWith('functions/')).length;
        const jobsCount = entryPoints.filter((p) => p.startsWith('jobs/')).length;
        diagnostics.push({
            severity: 'info',
            message: `[webstir-backend] entries by bucket: server=${server} functions=${functionsCount} jobs=${jobsCount}`
        });
    }
    catch {
        // best-effort only
    }
}
export function normalizeLogLevel(value) {
    if (typeof value !== 'string')
        return 'info';
    const v = value.toLowerCase();
    if (v === 'error' || v === 'warn' || v === 'info')
        return v;
    return 'info';
}
export function filterDiagnostics(list, min) {
    const rank = (s) => (s === 'error' ? 3 : s === 'warn' ? 2 : 1);
    const threshold = rank(min);
    return list.filter((d) => rank(d.severity) >= threshold);
}
