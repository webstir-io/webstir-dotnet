import path from 'node:path';
import type { BuilderContext } from '../builders/types.js';

interface Rule {
    readonly directory: string;
    readonly extensions?: readonly string[];
}

export function shouldProcess(context: BuilderContext, rules: readonly Rule[]): boolean {
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

export function isPathInside(target: string, directory: string): boolean {
    const relative = path.relative(directory, target);
    return relative === '' || (!relative.startsWith('..') && !path.isAbsolute(relative));
}
