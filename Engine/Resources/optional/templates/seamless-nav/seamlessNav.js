/**
 * Minimal PJAX-style navigation: swaps the <main> content, updates title/URL,
 * and restores scroll/focus. Use data-seamless on links to opt in.
 */
export function enableSeamlessNav(): void {
    document.addEventListener('click', async (event) => {
        const target = event.target as EventTarget | null;
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
        await navigateSeamlessly(link.href);
    });
}

async function navigateSeamlessly(url: string): Promise<void> {
    const response = await fetch(url, { headers: { 'X-Webstir-Seamless': '1' } });
    if (!response.ok) {
        window.location.href = url;
        return;
    }

    const html = await response.text();
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

    window.history.pushState({}, '', url);
    window.scrollTo({ top: 0, behavior: 'smooth' });
    const focusTarget = document.querySelector('[autofocus]');
    if (focusTarget instanceof HTMLElement) {
        focusTarget.focus();
    }
}
