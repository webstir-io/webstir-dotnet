import path from 'node:path';

import { glob } from 'glob';

export async function discoverEntryPoints(sourceRoot: string): Promise<string[]> {
    const patterns = [
        'index.{ts,tsx,js,mjs}',
        'functions/*/index.{ts,tsx,js,mjs}',
        'jobs/*/index.{ts,tsx,js,mjs}'
    ];
    const entries = new Set<string>();
    for (const pattern of patterns) {
        const matches = await glob(pattern, { cwd: sourceRoot, nodir: true, dot: false });
        for (const rel of matches) {
            entries.add(path.join(sourceRoot, rel));
        }
    }
    return Array.from(entries);
}
