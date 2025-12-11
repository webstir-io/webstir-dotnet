import path from 'node:path';
export function resolveWorkspacePaths(workspaceRoot) {
    return {
        sourceRoot: path.join(workspaceRoot, 'src', 'backend'),
        buildRoot: path.join(workspaceRoot, 'build', 'backend'),
        testsRoot: path.join(workspaceRoot, 'src', 'backend', 'tests')
    };
}
export function normalizeMode(rawMode) {
    if (typeof rawMode !== 'string') {
        return 'build';
    }
    const normalized = rawMode.toLowerCase();
    return normalized === 'publish' || normalized === 'test' ? normalized : 'build';
}
