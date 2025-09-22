import path from 'node:path';
export function shouldProcess(context, rules) {
    const changed = context.changedFile;
    if (!changed) {
        return true;
    }
    const normalizedChanged = path.resolve(changed);
    for (const rule of rules) {
        const normalizedDir = path.resolve(rule.directory);
        if (!isPathInside(normalizedChanged, normalizedDir)) {
            continue;
        }
        if (!rule.extensions || rule.extensions.length === 0) {
            return true;
        }
        const extension = path.extname(normalizedChanged).toLowerCase();
        if (rule.extensions.includes(extension)) {
            return true;
        }
    }
    return false;
}
export function isPathInside(target, directory) {
    const relative = path.relative(directory, target);
    return relative === '' || (!relative.startsWith('..') && !path.isAbsolute(relative));
}
