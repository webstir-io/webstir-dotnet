import path from 'node:path';

export function isInsideDirectory(filePath: string, directory: string): boolean {
    const resolvedFile = path.resolve(filePath);
    const resolvedDirectory = path.resolve(directory);
    const relative = path.relative(resolvedDirectory, resolvedFile);
    return relative === '' || (!relative.startsWith('..') && !path.isAbsolute(relative));
}

export function findPageFromChangedFile(changedFile: string | undefined, pagesRoot: string): string | null {
    if (!changedFile) {
        return null;
    }

    const resolvedChanged = path.resolve(changedFile);
    const resolvedPagesRoot = path.resolve(pagesRoot);
    if (!isInsideDirectory(resolvedChanged, resolvedPagesRoot)) {
        return null;
    }

    const relative = path.relative(resolvedPagesRoot, resolvedChanged);
    const segments = relative.split(path.sep);
    return segments.length > 0 && segments[0] ? segments[0] : null;
}

export function relativePathWithin(filePath: string | undefined, directory: string): string | null {
    if (!filePath) {
        return null;
    }

    if (!isInsideDirectory(filePath, directory)) {
        return null;
    }

    return path.relative(path.resolve(directory), path.resolve(filePath));
}
