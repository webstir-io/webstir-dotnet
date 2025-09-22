import path from 'node:path';
import { glob } from 'glob';
import { pathExists } from '../utils/fs.js';
export async function getPages(root) {
    const directories = await getPageDirectories(root);
    return directories.map((entry) => ({
        name: entry.name,
        directory: entry.directory
    }));
}
export async function getPageDirectories(root) {
    if (!(await pathExists(root))) {
        return [];
    }
    const entries = await glob('*/', { cwd: root, absolute: false, withFileTypes: false });
    return entries.map((entry) => {
        const name = entry.endsWith('/') ? entry.slice(0, -1) : entry;
        return {
            name,
            directory: path.join(root, name)
        };
    });
}
