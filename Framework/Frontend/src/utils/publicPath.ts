export function normalizeBasePath(value?: string | null): string {
    if (!value) {
        return '';
    }

    let trimmed = value.trim();
    if (!trimmed || trimmed === '/') {
        return '';
    }

    if (!trimmed.startsWith('/')) {
        trimmed = `/${trimmed}`;
    }

    while (trimmed.length > 1 && trimmed.endsWith('/')) {
        trimmed = trimmed.slice(0, -1);
    }

    return trimmed;
}

export function applyBasePath(value: string, basePath: string): string {
    if (!basePath) {
        return value;
    }

    if (!value.startsWith('/') || value.startsWith('//')) {
        return value;
    }

    if (isAlreadyBased(value, basePath)) {
        return value;
    }

    return `${basePath}${value}`;
}

export function stripBasePath(value: string, basePath: string): string {
    if (!basePath) {
        return value;
    }

    if (!value.startsWith('/')) {
        return value;
    }

    if (value === basePath) {
        return '/';
    }

    if (value.startsWith(`${basePath}/`) || value.startsWith(`${basePath}?`) || value.startsWith(`${basePath}#`)) {
        return value.slice(basePath.length);
    }

    return value;
}

function isAlreadyBased(value: string, basePath: string): boolean {
    return value === basePath
        || value.startsWith(`${basePath}/`)
        || value.startsWith(`${basePath}?`)
        || value.startsWith(`${basePath}#`);
}
