import { createDrawer, type DrawerController } from '../components/drawer.js';

export {};

type SearchScope = 'all' | 'docs' | 'page';

type SearchIndexEntry = {
    path: string;
    title: string;
    description?: string;
    excerpt: string;
    headings: string[];
    kind: 'docs' | 'page';
    haystack: string;
};

type SearchUiState = {
    entries: SearchIndexEntry[] | null;
    entriesPromise: Promise<SearchIndexEntry[]> | null;
    open: boolean;
    scope: SearchScope;
    trigger: HTMLButtonElement | null;
    lastActiveElement: HTMLElement | null;
};

declare global {
    interface Window {
        __webstirSearchUiV2?: SearchUiState;
    }
}

const BASE_PATH = resolveBasePath();

function getState(): SearchUiState {
    if (window.__webstirSearchUiV2) {
        return window.__webstirSearchUiV2;
    }

    const created: SearchUiState = {
        entries: null,
        entriesPromise: null,
        open: false,
        scope: 'all',
        trigger: null,
        lastActiveElement: null
    };

    window.__webstirSearchUiV2 = created;
    return created;
}

const state = getState();
let drawer: DrawerController | null = null;

function resolveBasePath(): string {
    const raw = document.documentElement?.getAttribute('data-webstir-base') ?? '';
    return normalizeBasePath(raw);
}

function normalizeBasePath(value: string): string {
    const trimmed = value.trim();
    if (!trimmed || trimmed === '/') {
        return '';
    }
    if (!trimmed.startsWith('/')) {
        return `/${trimmed}`;
    }
    return trimmed.endsWith('/') ? trimmed.slice(0, -1) : trimmed;
}

function withBasePath(value: string): string {
    if (!BASE_PATH) {
        return value;
    }
    if (!value.startsWith('/') || value.startsWith('//')) {
        return value;
    }
    if (value === BASE_PATH || value.startsWith(`${BASE_PATH}/`) || value.startsWith(`${BASE_PATH}?`) || value.startsWith(`${BASE_PATH}#`)) {
        return value;
    }
    return `${BASE_PATH}${value}`;
}

function escapeHtml(value: unknown): string {
    return String(value)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/\"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

function stripAndNormalize(value: unknown): string {
    return String(value).toLowerCase().replace(/\s+/g, ' ').trim();
}

function normalizeKind(kind: unknown): 'docs' | 'page' {
    const normalized = typeof kind === 'string' ? kind.toLowerCase().trim() : '';
    return normalized === 'page' ? 'page' : 'docs';
}

function computeScore(entry: SearchIndexEntry, query: string): number {
    let score = 0;

    if (entry.title && stripAndNormalize(entry.title).includes(query)) {
        score += 10;
    }
    if (entry.description && stripAndNormalize(entry.description).includes(query)) {
        score += 4;
    }
    if (entry.excerpt && stripAndNormalize(entry.excerpt).includes(query)) {
        score += 2;
    }

    if (Array.isArray(entry.headings) && entry.headings.some((h) => stripAndNormalize(h).includes(query))) {
        score += 6;
    }

    if (entry.kind === 'docs') {
        score += 1;
    }

    return score;
}

async function ensureIndexLoaded(): Promise<SearchIndexEntry[]> {
    if (state.entries !== null) {
        return state.entries;
    }

    state.entriesPromise ??= loadIndex();
    state.entries = await state.entriesPromise;
    return state.entries;
}

async function loadIndex(): Promise<SearchIndexEntry[]> {
    try {
        const response = await fetch(withBasePath('/search.json'), { headers: { Accept: 'application/json' } });
        if (!response.ok) {
            return [];
        }

        const data: unknown = await response.json();
        if (!Array.isArray(data)) {
            return [];
        }

        return data
            .filter((entry): entry is {
                path: string;
                title: string;
                description?: unknown;
                excerpt?: unknown;
                headings?: unknown;
                kind?: unknown;
            } => {
                if (!entry || typeof entry !== 'object') {
                    return false;
                }

                const record = entry as Record<string, unknown>;
                return typeof record.path === 'string' && typeof record.title === 'string';
            })
            .map((entry) => {
                const kind = normalizeKind(entry.kind);
                const headings = Array.isArray(entry.headings)
                    ? entry.headings.filter((h): h is string => typeof h === 'string')
                    : [];
                const haystack = stripAndNormalize(
                    `${entry.title} ${entry.description ?? ''} ${entry.excerpt ?? ''} ${headings.join(' ')}`
                );
                return {
                    path: entry.path,
                    title: entry.title,
                    description: typeof entry.description === 'string' ? entry.description : undefined,
                    excerpt: typeof entry.excerpt === 'string' ? entry.excerpt : '',
                    headings,
                    kind,
                    haystack
                };
            });
    } catch {
        return [];
    }
}

function parseScope(scope: string | null | undefined): SearchScope {
    if (scope === 'docs' || scope === 'page' || scope === 'all') {
        return scope;
    }
    return 'all';
}

function setScope(scope: SearchScope): void {
    state.scope = scope;

    const root = document.getElementById('webstir-search');
    if (!root) {
        return;
    }

    root.querySelectorAll<HTMLButtonElement>('.webstir-search__scopes button[data-scope]').forEach((button) => {
        const value = parseScope(button.getAttribute('data-scope'));
        button.setAttribute('aria-pressed', value === scope ? 'true' : 'false');
    });

    void refreshResults();
}

function getPlatformShortcut(): string {
    const platform = (navigator as unknown as { userAgentData?: { platform?: string } }).userAgentData?.platform
        ?? navigator.platform
        ?? '';
    return /mac/i.test(platform) ? '⌘K' : 'Ctrl K';
}

function ensureTrigger(): HTMLButtonElement | null {
    if (state.trigger && document.contains(state.trigger)) {
        return state.trigger;
    }

    const existing = document.querySelector<HTMLButtonElement>('[data-webstir-search-trigger="true"]');
    if (existing) {
        state.trigger = existing;
        return existing;
    }

    const menu = document.querySelector<HTMLElement>('[data-app-menu]');
    if (!menu) {
        return null;
    }

    const shortcut = getPlatformShortcut();
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'webstir-search__trigger ws-icon-button';
    button.innerHTML = [
        '<span class="webstir-search__trigger-icon" aria-hidden="true">',
        '  <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">',
        '    <circle cx="11" cy="11" r="7"></circle>',
        '    <path d="M20 20l-3.5-3.5"></path>',
        '  </svg>',
        '</span>'
    ].join('\n');
    button.setAttribute('data-webstir-search-trigger', 'true');
    button.setAttribute('aria-controls', 'webstir-search');
    button.setAttribute('aria-expanded', 'false');
    button.setAttribute('aria-label', `Search (${shortcut})`);
    button.setAttribute('title', `Search (${shortcut})`);

    const nav = menu.querySelector('.app-nav');
    if (nav) {
        nav.insertAdjacentElement('afterend', button);
    } else {
        menu.appendChild(button);
    }

    state.trigger = button;
    return button;
}

function ensureUi(): HTMLElement {
    let root = document.getElementById('webstir-search');
    if (root) {
        ensureBackdrop(root);
        return root;
    }

    root = document.createElement('div');
    root.id = 'webstir-search';
    root.innerHTML = [
        '<div class="ws-drawer-backdrop" data-webstir-search-close></div>',
        '<div class="webstir-search__drawer" role="dialog" aria-modal="true" aria-label="Search">',
        '  <div class="webstir-search__drawer-inner ws-container">',
        '    <div class="webstir-search__field">',
        '      <span class="webstir-search__icon" aria-hidden="true">',
        '        <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">',
        '          <circle cx="11" cy="11" r="7"></circle>',
        '          <path d="M20 20l-3.5-3.5"></path>',
        '        </svg>',
        '      </span>',
        '      <input class="webstir-search__input" type="search" placeholder="Search…" autocomplete="off" spellcheck="false" autocapitalize="none" />',
        '      <button type="button" class="webstir-search__close" data-webstir-search-close aria-label="Close search">',
        '        <span aria-hidden="true">Esc</span>',
        '      </button>',
        '    </div>',
        '    <div class="webstir-search__scopes" role="toolbar" aria-label="Search scope">',
        '      <button type="button" data-scope="all" aria-pressed="true">All</button>',
        '      <button type="button" data-scope="docs" aria-pressed="false">Docs</button>',
        '      <button type="button" data-scope="page" aria-pressed="false">Pages</button>',
        '    </div>',
        '    <div class="webstir-search__body">',
        '      <div class="webstir-search__hint" role="status" aria-live="polite"></div>',
        '      <div class="webstir-search__quicklinks" hidden>',
        '        <div class="webstir-search__label">Quick links</div>',
        '        <ul class="webstir-search__quicklinks-list"></ul>',
        '      </div>',
        '      <div class="webstir-search__matches" hidden>',
        '        <ul class="webstir-search__results-list"></ul>',
        '      </div>',
        '    </div>',
        '  </div>',
        '</div>'
    ].join('\n');

    document.body.appendChild(root);
    return root;
}

function ensureBackdrop(root: HTMLElement): void {
    const existing = root.querySelector<HTMLElement>('.ws-drawer-backdrop');
    if (existing) {
        return;
    }

    const legacy = root.querySelector<HTMLElement>('.webstir-search__backdrop');
    if (legacy) {
        legacy.classList.add('ws-drawer-backdrop');
        legacy.classList.remove('webstir-search__backdrop');
        legacy.setAttribute('data-webstir-search-close', '');
        legacy.setAttribute('aria-hidden', 'true');
        return;
    }

    const created = document.createElement('div');
    created.className = 'ws-drawer-backdrop';
    created.setAttribute('data-webstir-search-close', '');
    created.setAttribute('aria-hidden', 'true');
    root.prepend(created);
}

function openSearch(options?: { initialQuery?: string }): void {
    if (state.open) {
        const input = document.querySelector<HTMLInputElement>('#webstir-search .webstir-search__input');
        input?.focus();
        input?.select();
        return;
    }

    const root = ensureUi();
    const activeDrawer = drawer ?? createDrawer({
        root,
        openAttribute: 'data-open',
        bodyClass: 'webstir-search-open',
        overlay: {
            headerSelector: '.app-header',
            target: root,
            varName: '--ws-drawer-top'
        },
        closeOnEscape: true,
        trapFocus: true,
        onOpen: () => {
            state.open = true;
            const trigger = ensureTrigger();
            trigger?.setAttribute('aria-expanded', 'true');
        },
        onClose: () => {
            state.open = false;
            const trigger = ensureTrigger();
            trigger?.setAttribute('aria-expanded', 'false');
            state.lastActiveElement?.focus?.();
            state.lastActiveElement = null;
        }
    });
    drawer = activeDrawer;
    activeDrawer.syncOverlayOffset();

    state.lastActiveElement = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    activeDrawer.open();

    const menu = document.querySelector<HTMLElement>('[data-app-menu]');
    if (menu?.classList.contains('is-open')) {
        menu.querySelector<HTMLButtonElement>('.app-menu__toggle')?.click();
    }

    const input = root.querySelector<HTMLInputElement>('.webstir-search__input');
    if (input) {
        if (options?.initialQuery) {
            input.value = options.initialQuery;
        }
        input.focus();
        input.select();
    }

    void refreshResults();
}

function closeSearch(): void {
    if (!state.open) {
        return;
    }

    drawer?.close();
}

function toggleSearch(): void {
    if (state.open) {
        closeSearch();
    } else {
        openSearch();
    }
}

function renderQuickLinks(): string {
    const links = Array.from(document.querySelectorAll<HTMLAnchorElement>('.app-nav a'))
        .filter((link) => !!link.getAttribute('href'))
        .slice(0, 6);

    if (links.length === 0) {
        return '<li class="webstir-search__empty">Search docs and pages.</li>';
    }

    return links
        .map((link) => {
            const href = escapeHtml(withBasePath(link.getAttribute('href') ?? '#'));
            const label = escapeHtml(link.textContent ?? '');
            return `<li><a href="${href}"><span>${label}</span><span class="webstir-search__arrow" aria-hidden="true">→</span></a></li>`;
        })
        .join('');
}

async function refreshResults(): Promise<void> {
    const root = document.getElementById('webstir-search');
    if (!root) {
        return;
    }

    const input = root.querySelector<HTMLInputElement>('.webstir-search__input');
    const hint = root.querySelector<HTMLElement>('.webstir-search__hint');
    const quickLinks = root.querySelector<HTMLElement>('.webstir-search__quicklinks');
    const quickLinksList = root.querySelector<HTMLElement>('.webstir-search__quicklinks-list');
    const matchesRoot = root.querySelector<HTMLElement>('.webstir-search__matches');
    const resultsList = root.querySelector<HTMLElement>('.webstir-search__results-list');
    const scopes = root.querySelector<HTMLElement>('.webstir-search__scopes');

    if (!input || !hint || !quickLinks || !quickLinksList || !matchesRoot || !resultsList || !scopes) {
        return;
    }

    const entries = await ensureIndexLoaded();
    const kinds = new Set(entries.map((e) => e.kind));

    const query = stripAndNormalize(input.value);
    const showScopes = kinds.size > 1 && query.length >= 2;
    if (showScopes) {
        scopes.removeAttribute('hidden');
    } else {
        scopes.setAttribute('hidden', '');
    }

    if (!query) {
        hint.textContent = '';
        quickLinksList.innerHTML = renderQuickLinks();
        quickLinks.removeAttribute('hidden');
        matchesRoot.setAttribute('hidden', '');
        resultsList.innerHTML = '';
        return;
    }

    quickLinks.setAttribute('hidden', '');

    if (query.length < 2) {
        hint.textContent = 'Search docs and pages.';
        matchesRoot.setAttribute('hidden', '');
        resultsList.innerHTML = '';
        return;
    }

    const matches = entries
        .filter((entry) => (state.scope === 'all' ? true : entry.kind === state.scope))
        .filter((entry) => entry.haystack.includes(query))
        .map((entry) => ({ entry, score: computeScore(entry, query) }))
        .sort((a, b) => {
            if (a.score !== b.score) {
                return b.score - a.score;
            }
            return String(a.entry.title).localeCompare(String(b.entry.title));
        })
        .map((item) => item.entry);

    matchesRoot.removeAttribute('hidden');

    if (matches.length === 0) {
        hint.textContent = '';
        resultsList.innerHTML = '<li class="webstir-search__empty">No results.</li>';
        return;
    }

    hint.textContent = '';

    const renderEntry = (entry: SearchIndexEntry): string => {
        const title = escapeHtml(entry.title);
        const excerpt = escapeHtml(entry.excerpt);
        const href = escapeHtml(withBasePath(entry.path));
        return `<li><a href="${href}"><strong>${title}</strong><span>${excerpt}</span></a></li>`;
    };

    const shouldGroup = state.scope === 'all' && kinds.size > 1;
    if (!shouldGroup) {
        resultsList.innerHTML = matches.slice(0, 12).map(renderEntry).join('');
        return;
    }

    const docs = matches.filter((m) => m.kind === 'docs').slice(0, 6);
    const pages = matches.filter((m) => m.kind === 'page').slice(0, 6);

    const sections: string[] = [];
    if (docs.length > 0) {
        sections.push('<li class="webstir-search__group">Docs</li>');
        sections.push(...docs.map(renderEntry));
    }
    if (pages.length > 0) {
        sections.push('<li class="webstir-search__group">Pages</li>');
        sections.push(...pages.map(renderEntry));
    }

    resultsList.innerHTML = sections.join('');
}

function getResultLinks(root: HTMLElement): HTMLAnchorElement[] {
    const links = Array.from(
        root.querySelectorAll<HTMLAnchorElement>('.webstir-search__results-list a, .webstir-search__quicklinks-list a')
    );
    return links.filter((link) => !link.closest('[hidden]') && link.offsetParent !== null);
}

function boot(): void {
    const root = ensureUi();

    const trigger = ensureTrigger();
    if (trigger) {
        trigger.addEventListener('click', () => toggleSearch());
    }

    document.addEventListener('keydown', (event) => {
        if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
            event.preventDefault();
            toggleSearch();
        }
    });

    root.addEventListener('keydown', (event) => {
        const target = event.target;
        if (target instanceof HTMLAnchorElement
            && target.matches('.webstir-search__results-list a, .webstir-search__quicklinks-list a')) {
            if (event.key === 'ArrowDown' || event.key === 'ArrowUp' || event.key === 'Home' || event.key === 'End') {
                const links = getResultLinks(root);
                const index = links.indexOf(target);
                if (index >= 0) {
                    let nextIndex = index;
                    if (event.key === 'Home') {
                        nextIndex = 0;
                    } else if (event.key === 'End') {
                        nextIndex = links.length - 1;
                    } else if (event.key === 'ArrowDown') {
                        nextIndex = Math.min(index + 1, links.length - 1);
                    } else {
                        nextIndex = Math.max(index - 1, 0);
                    }
                    event.preventDefault();
                    links[nextIndex]?.focus();
                }
                return;
            }
        }

    });

    root.addEventListener('click', (event) => {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }

        if (target.closest('[data-webstir-search-close]')) {
            closeSearch();
            return;
        }

        if (target.matches('.ws-drawer-backdrop')) {
            closeSearch();
            return;
        }

        const resultLink = target.closest('.webstir-search__results-list a, .webstir-search__quicklinks-list a');
        if (resultLink) {
            closeSearch();
        }
    });

    root.querySelectorAll<HTMLButtonElement>('.webstir-search__scopes button[data-scope]').forEach((button) => {
        button.addEventListener('click', () => setScope(parseScope(button.getAttribute('data-scope'))));
    });

    const input = root.querySelector<HTMLInputElement>('.webstir-search__input');
    if (input) {
        input.addEventListener('input', () => void refreshResults());
        input.addEventListener('keydown', (event) => {
            if (event.key !== 'ArrowDown' && event.key !== 'ArrowUp') {
                return;
            }

            const links = getResultLinks(root);
            if (links.length > 0) {
                event.preventDefault();
                const target = event.key === 'ArrowDown' ? links[0] : links[links.length - 1];
                target?.focus();
            }
        });
    }

    window.addEventListener('resize', () => {
        if (!state.open) {
            return;
        }

        drawer?.syncOverlayOffset();
    });

    window.addEventListener('webstir:client-nav', () => {
        if (state.open) {
            closeSearch();
        }
    });
}

boot();
