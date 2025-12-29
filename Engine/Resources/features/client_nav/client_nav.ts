export {};

/**
 * Minimal PJAX-style navigation: swaps the <main> content, updates title/URL,
 * and restores scroll/focus.
 *
 * Opt out per-link with:
 * - data-no-client-nav
 * - data-client-nav="off"
 */
export function enableClientNav(): void {
    document.addEventListener('click', async (event) => {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }
        if (event.defaultPrevented || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
            return;
        }

        const link = target.closest('a');
        if (!link || !(link instanceof HTMLAnchorElement)) {
            return;
        }

        const setting = link.getAttribute('data-client-nav');
        const optOut = link.hasAttribute('data-no-client-nav')
            || setting === 'off'
            || setting === 'false';
        if (optOut) {
            return;
        }

        const isExternal = link.origin !== window.location.origin;
        const opensInNewTab = link.getAttribute('target') === '_blank';
        const isDownload = link.hasAttribute('download');
        if (isExternal || opensInNewTab || isDownload) {
            return;
        }

        const isSameDocumentAnchor = link.hash
            && link.pathname === window.location.pathname
            && link.search === window.location.search;
        if (isSameDocumentAnchor) {
            return;
        }

        event.preventDefault();
        await renderUrl(link.href, { pushHistory: true });
    });

    window.addEventListener('popstate', async () => {
        await renderUrl(window.location.href, { pushHistory: false });
    });
}

let activeRequestId = 0;
let activeController: AbortController | null = null;
const DYNAMIC_ATTR = 'data-webstir-dynamic';
const DYNAMIC_VALUE = 'client-nav';

async function renderUrl(url: string, { pushHistory }: { pushHistory: boolean }): Promise<void> {
    activeRequestId += 1;
    const requestId = activeRequestId;

    if (activeController) {
        activeController.abort();
    }

    const controller = new AbortController();
    activeController = controller;

    let response: Response;
    try {
        response = await fetch(url, {
            headers: { 'X-Webstir-Client-Nav': '1' },
            signal: controller.signal
        });
    } catch {
        if (controller.signal.aborted) {
            return;
        }

        window.location.href = url;
        return;
    }

    if (!response.ok) {
        window.location.href = url;
        return;
    }

    const html = await response.text();
    if (requestId !== activeRequestId) {
        return;
    }

    const parser = new DOMParser();
    const doc = parser.parseFromString(html, 'text/html');

    syncHead(doc, url);

    const newMain = doc.querySelector('main');
    const currentMain = document.querySelector('main');
    if (newMain && currentMain) {
        currentMain.replaceWith(newMain);
    }

    const newTitle = doc.querySelector('title');
    if (newTitle && newTitle.textContent) {
        document.title = newTitle.textContent;
    }

    if (pushHistory) {
        window.history.pushState({}, '', url);
    }
    window.scrollTo({ top: 0, behavior: 'smooth' });
    const focusTarget = document.querySelector('[autofocus]');
    if (focusTarget instanceof HTMLElement) {
        focusTarget.focus();
    }

    executeScripts(document.querySelector('main'));
    window.dispatchEvent(new CustomEvent('webstir:client-nav', { detail: { url } }));
}

enableClientNav();

function syncHead(doc: Document, url: string): void {
    const head = document.head;
    const newHead = doc.head;
    if (!head || !newHead) {
        return;
    }

    const preservedClientNav = head.querySelector('script[data-webstir="client-nav"]');
    const preservedAppCss = Array.from(head.querySelectorAll<HTMLLinkElement>('link[rel="stylesheet"]'))
        .find((link) => isAppStylesheetHref(link.getAttribute('href'))) ?? null;

    for (const element of Array.from(head.querySelectorAll(`script[${DYNAMIC_ATTR}="${DYNAMIC_VALUE}"]`))) {
        element.remove();
    }

    for (const script of Array.from(head.querySelectorAll('script[src]'))) {
        const src = script.getAttribute('src') ?? '';
        if (script === preservedClientNav) {
            continue;
        }
        if (src === '/hmr.js' || src === '/refresh.js') {
            continue;
        }
        if (src.startsWith('/pages/')) {
            script.remove();
        }
    }

    const desiredStyles = new Map<string, string>();
    for (const link of Array.from(newHead.querySelectorAll<HTMLLinkElement>('link[rel="stylesheet"]'))) {
        const href = link.getAttribute('href');
        if (!href) {
            continue;
        }
        const resolved = resolveUrl(href, url);
        if (!resolved) {
            continue;
        }
        const key = stripQueryAndHash(resolved);
        const finalHref = key === '/app/app.css' && preservedAppCss
            ? (preservedAppCss.getAttribute('href') ?? resolved)
            : resolved;
        desiredStyles.set(key, finalHref);
    }

    if (preservedAppCss) {
        const appHref = preservedAppCss.getAttribute('href') ?? '/app/app.css';
        desiredStyles.set('/app/app.css', appHref);
    }

    const existingStyles = new Map<string, HTMLLinkElement>();
    for (const link of Array.from(head.querySelectorAll<HTMLLinkElement>('link[rel="stylesheet"]'))) {
        const key = normalizeStylesheetKey(link.getAttribute('href'), window.location.href);
        if (!key) {
            link.remove();
            continue;
        }
        if (desiredStyles.has(key)) {
            if (!existingStyles.has(key)) {
                existingStyles.set(key, link);
            }
            continue;
        }
        link.remove();
    }

    for (const [key, href] of desiredStyles.entries()) {
        if (existingStyles.has(key)) {
            continue;
        }
        const next = document.createElement('link');
        next.rel = 'stylesheet';
        next.href = href;
        head.appendChild(next);
        existingStyles.set(key, next);
    }

    for (const script of Array.from(newHead.querySelectorAll('script[src]'))) {
        const src = script.getAttribute('src');
        if (!src) {
            continue;
        }
        if (src === '/clientNav.js' || src.endsWith('/clientNav.js')) {
            continue;
        }
        if (src === '/hmr.js' || src === '/refresh.js') {
            continue;
        }

        const resolved = resolveUrl(src, url);
        if (!resolved) {
            continue;
        }

        const next = document.createElement('script');
        const type = script.getAttribute('type');
        if (type) {
            next.type = type;
        }
        next.src = resolved;
        next.setAttribute(DYNAMIC_ATTR, DYNAMIC_VALUE);
        head.appendChild(next);
    }

    if (preservedClientNav && !head.contains(preservedClientNav)) {
        head.appendChild(preservedClientNav);
    }
}

function executeScripts(container: Element | null): void {
    if (!container) {
        return;
    }

    const scripts = Array.from(container.querySelectorAll('script'));
    for (const script of scripts) {
        const src = script.getAttribute('src');
        const type = script.getAttribute('type');

        if (src && (src === '/clientNav.js' || src.endsWith('/clientNav.js'))) {
            script.remove();
            continue;
        }
        if (src === '/hmr.js' || src === '/refresh.js') {
            script.remove();
            continue;
        }

        const next = document.createElement('script');
        if (type) {
            next.type = type;
        }

        if (src) {
            const resolved = resolveUrl(src, window.location.href);
            if (resolved) {
                next.src = resolved;
            }
        } else if (script.textContent) {
            next.textContent = script.textContent;
        }

        script.replaceWith(next);
    }
}

function resolveUrl(value: string, baseUrl: string): string | null {
    try {
        const trimmed = String(value ?? '').trim();
        const [path, suffix] = splitPathSuffix(trimmed);
        if (path && !path.startsWith('/') && !path.startsWith('http:') && !path.startsWith('https:')) {
            if (path === 'index.js' || path === 'index.css') {
                const pageName = getPageNameFromUrl(baseUrl);
                return `/pages/${pageName}/${path}${suffix}`;
            }
        }

        const resolved = new URL(value, baseUrl);
        return resolved.pathname + resolved.search + resolved.hash;
    } catch {
        return null;
    }
}

function normalizeStylesheetKey(href: string | null, baseUrl: string): string | null {
    const resolved = resolveUrl(href ?? '', baseUrl);
    if (!resolved) {
        return null;
    }
    return stripQueryAndHash(resolved);
}

function stripQueryAndHash(value: string): string {
    return value.split(/[?#]/)[0] ?? value;
}

function splitPathSuffix(value: string): [string, string] {
    const [path, suffix = ''] = value.split(/(?=[?#])/);
    return [path ?? '', suffix ?? ''];
}

function isAppStylesheetHref(href: string | null): boolean {
    if (!href) {
        return false;
    }

    try {
        return new URL(href, window.location.origin).pathname === '/app/app.css';
    } catch {
        const trimmed = href.trim();
        if (!trimmed) {
            return false;
        }
        const [path] = trimmed.split(/[?#]/);
        return path === '/app/app.css';
    }
}

function getPageNameFromUrl(url: string): string {
    try {
        const pathname = new URL(url, window.location.href).pathname;
        const trimmed = pathname.replace(/^\/+|\/+$/g, '');
        if (!trimmed) {
            return 'home';
        }

        const firstSegment = trimmed.split('/')[0];
        return firstSegment || 'home';
    } catch {
        return 'home';
    }
}

function cssEscape(value: string): string {
    if (typeof CSS !== 'undefined' && typeof (CSS as { escape?: (value: string) => string }).escape === 'function') {
        return (CSS as { escape: (value: string) => string }).escape(value);
    }
    return value.replace(/[\"\\\\]/g, '\\\\$&');
}
