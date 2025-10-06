import path from 'node:path';
import { FOLDERS, EXTENSIONS } from '../core/constants.js';
import { pathExists, readFile, stat } from '../utils/fs.js';
const INLINE_THRESHOLD_BYTES = 6 * 1024;
export async function inlineCriticalCss(document, pageName, distRoot, cssFile) {
    if (!cssFile) {
        return;
    }
    const cssPath = path.join(distRoot, FOLDERS.pages, pageName, cssFile);
    if (!(await pathExists(cssPath))) {
        return;
    }
    const info = await stat(cssPath).catch(() => null);
    if (!info || !info.isFile() || info.size > INLINE_THRESHOLD_BYTES) {
        return;
    }
    const cssContent = await readFile(cssPath);
    const head = document('head').first();
    if (head.length === 0) {
        return;
    }
    const href = `/${FOLDERS.pages}/${pageName}/${cssFile}`;
    document(`link[href="${href}"]`).remove();
    if (cssFile.endsWith(EXTENSIONS.css)) {
        document(`link[rel="preload"][href="${href}"]`).remove();
    }
    head.append(`\n<style data-critical>\n${cssContent}\n</style>\n`);
}
