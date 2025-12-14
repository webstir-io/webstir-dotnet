/**
 * Minimal PJAX-style navigation: swaps the <main> content, updates title/URL,
 * and restores scroll/focus. Use data-seamless on links to opt in.
 */
export function enableSeamlessNav() {
    document.addEventListener('click', async (event) => {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }
        if (event.defaultPrevented || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
            return;
        }

        const link = target.closest('a[data-seamless]');
        if (!link || !(link instanceof HTMLAnchorElement)) {
            return;
        }

        const isExternal = link.origin !== window.location.origin;
        const opensInNewTab = link.getAttribute('target') === '_blank';
        if (isExternal || opensInNewTab) {
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
let activeController = null;

async function renderUrl(url, { pushHistory }) {
    activeRequestId += 1;
    const requestId = activeRequestId;

    if (activeController) {
        activeController.abort();
    }

    const controller = new AbortController();
    activeController = controller;

    let response;
    try {
        response = await fetch(url, {
            headers: { 'X-Webstir-Seamless': '1' },
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

    window.dispatchEvent(new CustomEvent('webstir:seamless-nav', { detail: { url } }));
}

const demoShellId = createShortId();
let demoNavCount = 0;

function updateDemoIndicators() {
    const sessionEl = document.getElementById('webstir-seamless-session');
    if (sessionEl) {
        sessionEl.textContent = demoShellId;
    }

    const countEl = document.getElementById('webstir-seamless-nav-count');
    if (countEl) {
        countEl.textContent = String(demoNavCount);
    }
}

function createShortId() {
    return Math.random().toString(16).slice(2, 10);
}

updateDemoIndicators();
window.addEventListener('webstir:seamless-nav', () => {
    demoNavCount += 1;
    updateDemoIndicators();
});
enableSeamlessNav();
