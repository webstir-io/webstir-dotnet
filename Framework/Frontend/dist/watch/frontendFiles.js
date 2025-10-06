import path from 'node:path';
import { ensureDir, pathExists, copy } from '../utils/fs.js';
import { FILES, EXTENSIONS } from '../core/constants.js';
export async function resolveEntryPoint(pageDirectory) {
    const candidates = [`${FILES.index}${EXTENSIONS.ts}`, `${FILES.index}.tsx`, `${FILES.index}${EXTENSIONS.js}`, `${FILES.index}.jsx`];
    for (const candidate of candidates) {
        const file = path.join(pageDirectory, candidate);
        if (await pathExists(file)) {
            return file;
        }
    }
    return null;
}
export async function copyRefreshScript(config) {
    const runtimeScripts = [FILES.refreshJs, FILES.hmrJs];
    for (const scriptName of runtimeScripts) {
        const source = path.join(config.paths.src.app, scriptName);
        if (!(await pathExists(source))) {
            continue;
        }
        const destination = path.join(config.paths.build.frontend, scriptName);
        await ensureDir(path.dirname(destination));
        await copy(source, destination);
    }
}
