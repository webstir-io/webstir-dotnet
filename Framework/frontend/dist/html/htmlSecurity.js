import { createHash } from 'node:crypto';
const HTTP_TIMEOUT_MS = 5000;
export async function addSubresourceIntegrity(document) {
    const failures = [];
    await Promise.all([
        processScripts(document, failures),
        processStylesheets(document, failures)
    ]);
    return { failures };
}
async function processScripts(document, failures) {
    const scripts = document('script[src]').toArray();
    await Promise.all(scripts.map(async (element) => {
        const script = document(element);
        const src = script.attr('src');
        if (!src || !isExternal(src) || script.attr('integrity')) {
            return;
        }
        const sri = await fetchIntegrity(src);
        if (!sri) {
            failures.push(src);
            return;
        }
        script.attr('integrity', sri);
        if (!script.attr('crossorigin')) {
            script.attr('crossorigin', 'anonymous');
        }
    }));
}
async function processStylesheets(document, failures) {
    const links = document('link[rel="stylesheet"][href]').toArray();
    await Promise.all(links.map(async (element) => {
        const link = document(element);
        const href = link.attr('href');
        if (!href || !isExternal(href) || link.attr('integrity')) {
            return;
        }
        const sri = await fetchIntegrity(href);
        if (!sri) {
            failures.push(href);
            return;
        }
        link.attr('integrity', sri);
        if (!link.attr('crossorigin')) {
            link.attr('crossorigin', 'anonymous');
        }
    }));
}
function isExternal(url) {
    return url.startsWith('http://') || url.startsWith('https://') || url.startsWith('//');
}
async function fetchIntegrity(url) {
    try {
        const normalizedUrl = url.startsWith('//') ? `https:${url}` : url;
        const controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), HTTP_TIMEOUT_MS);
        try {
            const response = await fetch(normalizedUrl, { signal: controller.signal });
            if (!response.ok) {
                return null;
            }
            const arrayBuffer = await response.arrayBuffer();
            const hash = createHash('sha384').update(Buffer.from(arrayBuffer)).digest('base64');
            return `sha384-${hash}`;
        }
        finally {
            clearTimeout(timeout);
        }
    }
    catch {
        return null;
    }
}
