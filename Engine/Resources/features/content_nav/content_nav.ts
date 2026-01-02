export {};

type DocsNavEntry = {
    path: string;
    title: string;
    section?: string;
    order?: number;
};

type NavNode = {
    segment: string;
    path: string;
    title: string;
    children: NavNode[];
    isPage: boolean;
    position: number;
};

type ContentNavState = {
    navEntries?: DocsNavEntry[];
    cleanup?: () => void;
};

const STATE_KEY = '__webstirContentNavState';
const NAV_URL = '/docs-nav.json';
const NAV_LAYOUT_SELECTOR = '[data-content-nav="true"]';

const mediaQuery = window.matchMedia('(max-width: 68.75rem)');

function getState(): ContentNavState {
    const w = window as unknown as Record<string, ContentNavState | undefined>;
    if (!w[STATE_KEY]) {
        w[STATE_KEY] = {};
    }
    return w[STATE_KEY] as ContentNavState;
}

function normalizeDocsPath(pathname: string): string {
    if (!pathname.startsWith('/docs')) {
        return pathname;
    }
    if (pathname === '/docs') {
        return '/docs/';
    }
    return pathname.endsWith('/') ? pathname : `${pathname}/`;
}

async function fetchDocsNav(): Promise<DocsNavEntry[]> {
    const state = getState();
    if (state.navEntries) {
        return state.navEntries;
    }

    try {
        const response = await fetch(NAV_URL, { headers: { Accept: 'application/json' } });
        if (!response.ok) {
            state.navEntries = [];
            return [];
        }

        const payload = await response.json();
        if (!Array.isArray(payload)) {
            state.navEntries = [];
            return [];
        }

        const entries = payload
            .filter((entry): entry is DocsNavEntry => Boolean(entry && entry.path && entry.title))
            .map((entry) => ({
                path: String(entry.path),
                title: String(entry.title),
                section: typeof entry.section === 'string' ? entry.section : undefined,
                order: typeof entry.order === 'number' ? entry.order : undefined
            }));

        state.navEntries = entries;
        return entries;
    } catch {
        state.navEntries = [];
        return [];
    }
}

function buildNavTree(entries: readonly DocsNavEntry[]): NavNode {
    let position = 0;
    const root: NavNode = {
        segment: 'docs',
        path: '/docs/',
        title: 'Docs',
        children: [],
        isPage: false,
        position: position++
    };

    for (const entry of entries) {
        const normalizedPath = normalizeDocsPath(entry.path);
        const segments = normalizedPath.split('/').filter(Boolean);
        if (segments.length === 0) {
            continue;
        }

        let current = root;
        for (let index = 1; index < segments.length; index += 1) {
            const segment = segments[index];
            const nodePath = `/${segments.slice(0, index + 1).join('/')}/`;
            let child = current.children.find((node) => node.segment === segment);
            if (!child) {
                child = {
                    segment,
                    path: nodePath,
                    title: toTitleCase(segment.replace(/[-_]/g, ' ')),
                    children: [],
                    isPage: false,
                    position: position++
                };
                current.children.push(child);
            }
            current = child;
        }

        current.title = entry.title;
        current.isPage = true;
    }

    return root;
}

function renderNavList(nodes: readonly NavNode[], currentPath: string, depth = 0): HTMLOListElement {
    const list = document.createElement('ol');
    list.className = depth === 0 ? 'docs-nav__list' : 'docs-nav__list docs-nav__list--nested';

    const sorted = [...nodes].sort((a, b) => a.position - b.position);
    for (const node of sorted) {
        const item = document.createElement('li');
        item.className = 'docs-nav__item';

        const isActive = node.path === currentPath;
        const isBranch = !isActive && currentPath.startsWith(node.path);
        if (isActive) {
            item.dataset.active = 'true';
        } else if (isBranch) {
            item.dataset.activeBranch = 'true';
        }

        if (node.isPage) {
            const link = document.createElement('a');
            link.className = 'docs-nav__link';
            link.href = node.path;
            link.textContent = node.title;
            if (isActive) {
                link.setAttribute('aria-current', 'page');
            }
            item.appendChild(link);
        } else {
            const label = document.createElement('span');
            label.className = 'docs-nav__label';
            label.textContent = node.title;
            item.appendChild(label);
        }

        if (node.children.length > 0) {
            item.appendChild(renderNavList(node.children, currentPath, depth + 1));
        }

        list.appendChild(item);
    }

    return list;
}

function renderBreadcrumb(root: HTMLElement, titleByPath: ReadonlyMap<string, string>, currentPath: string): void {
    if (!currentPath.startsWith('/docs/')) {
        root.hidden = true;
        return;
    }

    const list = document.createElement('ol');
    list.className = 'docs-breadcrumb__list';

    const segments = currentPath.replace(/^\/docs\/?/, '').split('/').filter(Boolean);
    const crumbs: Array<{ title: string; href: string }> = [];

    const rootTitle = titleByPath.get('/docs/') ?? 'Docs';
    crumbs.push({ title: rootTitle, href: '/docs/' });

    let current = '/docs/';
    for (const segment of segments) {
        current = `${current}${segment}/`;
        const title = titleByPath.get(current) ?? toTitleCase(segment.replace(/[-_]/g, ' '));
        crumbs.push({ title, href: current });
    }

    for (let index = 0; index < crumbs.length; index += 1) {
        const crumb = crumbs[index];
        const item = document.createElement('li');
        item.className = 'docs-breadcrumb__item';

        if (index === crumbs.length - 1) {
            const label = document.createElement('span');
            label.textContent = crumb.title;
            label.setAttribute('aria-current', 'page');
            item.appendChild(label);
        } else {
            const link = document.createElement('a');
            link.className = 'docs-breadcrumb__link';
            link.href = crumb.href;
            link.textContent = crumb.title;
            item.appendChild(link);
        }

        list.appendChild(item);
    }

    root.innerHTML = '';
    root.appendChild(list);
    root.hidden = false;
}

function renderToc(
    tocRoot: HTMLElement,
    article: HTMLElement
): { headings: HTMLElement[]; links: Map<string, HTMLAnchorElement> } {
    const headings = Array.from(article.querySelectorAll<HTMLElement>('h2'))
        .filter((heading) => heading.textContent && heading.textContent.trim().length > 0);

    const list = document.createElement('ol');
    list.className = 'docs-toc__list';
    const links = new Map<string, HTMLAnchorElement>();

    for (const heading of headings) {
        const id = ensureHeadingId(heading);
        const item = document.createElement('li');
        const link = document.createElement('a');
        link.className = 'docs-toc__link';
        link.href = `#${id}`;
        link.textContent = heading.textContent?.trim() ?? '';
        item.appendChild(link);
        list.appendChild(item);
        links.set(id, link);
    }

    tocRoot.innerHTML = '';
    if (headings.length > 0) {
        tocRoot.appendChild(list);
        tocRoot.hidden = false;
    } else {
        tocRoot.hidden = true;
    }

    return { headings, links };
}

function ensureHeadingId(heading: HTMLElement): string {
    const existing = heading.id?.trim();
    if (existing) {
        return existing;
    }

    const text = heading.textContent?.trim() ?? '';
    const base = slugifyHeading(text) || 'section';
    let candidate = base;
    let counter = 2;
    while (document.getElementById(candidate)) {
        candidate = `${base}-${counter}`;
        counter += 1;
    }
    heading.id = candidate;
    return candidate;
}

function slugifyHeading(value: string): string {
    return value
        .trim()
        .toLowerCase()
        .replace(/['"]/g, '')
        .replace(/[^a-z0-9\s-]/g, '')
        .replace(/\s+/g, '-')
        .replace(/-+/g, '-');
}

function toTitleCase(value: string): string {
    return value
        .split(/\s+/)
        .filter((part) => part.length > 0)
        .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
        .join(' ');
}

function getScrollSpyOffset(): number {
    const header = document.querySelector('.app-header');
    const height = header instanceof HTMLElement ? header.getBoundingClientRect().height : 0;
    return height + 16;
}

function setupScrollSpy(headings: HTMLElement[], links: Map<string, HTMLAnchorElement>): () => void {
    if (headings.length === 0 || links.size === 0) {
        return () => undefined;
    }

    let rafId = 0;

    const setActive = (activeId: string | null): void => {
        for (const [id, link] of links) {
            if (id === activeId) {
                link.dataset.active = 'true';
            } else {
                link.dataset.active = 'false';
            }
        }
    };

    const update = (): void => {
        const offset = getScrollSpyOffset();
        let activeId: string | null = null;

        for (const heading of headings) {
            const top = heading.getBoundingClientRect().top - offset;
            if (top <= 0) {
                activeId = heading.id;
            } else {
                break;
            }
        }

        if (!activeId && headings.length > 0) {
            activeId = headings[0].id;
        }

        setActive(activeId);
    };

    const onScroll = (): void => {
        if (rafId) {
            return;
        }
        rafId = window.requestAnimationFrame(() => {
            rafId = 0;
            update();
        });
    };

    window.addEventListener('scroll', onScroll, { passive: true });
    window.addEventListener('resize', onScroll);
    onScroll();

    return () => {
        window.removeEventListener('scroll', onScroll);
        window.removeEventListener('resize', onScroll);
        if (rafId) {
            window.cancelAnimationFrame(rafId);
        }
    };
}

function setPanelState(
    panel: HTMLElement | null,
    toggle: HTMLButtonElement | null,
    open: boolean
): void {
    if (!panel) {
        return;
    }
    panel.dataset.open = open ? 'true' : 'false';
    if (toggle) {
        toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
    }
}

function setupPanelToggles(
    layout: HTMLElement,
    hasNav: boolean,
    hasToc: boolean
): () => void {
    const sidebar = layout.querySelector<HTMLElement>('[data-docs-sidebar]');
    const tocPanel = layout.querySelector<HTMLElement>('[data-docs-toc]');
    const navToggle = layout.querySelector<HTMLButtonElement>('[data-docs-nav-toggle]');
    const tocToggle = layout.querySelector<HTMLButtonElement>('[data-docs-toc-toggle]');

    if (navToggle) {
        navToggle.hidden = !hasNav;
    }
    if (tocToggle) {
        tocToggle.hidden = !hasToc;
    }

    const applyViewportState = (): void => {
        const open = !mediaQuery.matches;
        setPanelState(sidebar, navToggle ?? null, open && hasNav);
        setPanelState(tocPanel, tocToggle ?? null, open && hasToc);
    };

    const onNavToggle = (): void => {
        if (!sidebar || !hasNav) {
            return;
        }
        const next = sidebar.dataset.open !== 'true';
        setPanelState(sidebar, navToggle ?? null, next);
    };

    const onTocToggle = (): void => {
        if (!tocPanel || !hasToc) {
            return;
        }
        const next = tocPanel.dataset.open !== 'true';
        setPanelState(tocPanel, tocToggle ?? null, next);
    };

    navToggle?.addEventListener('click', onNavToggle);
    tocToggle?.addEventListener('click', onTocToggle);
    mediaQuery.addEventListener('change', applyViewportState);
    applyViewportState();

    return () => {
        navToggle?.removeEventListener('click', onNavToggle);
        tocToggle?.removeEventListener('click', onTocToggle);
        mediaQuery.removeEventListener('change', applyViewportState);
    };
}

async function initContentNav(): Promise<void> {
    const layouts = Array.from(document.querySelectorAll<HTMLElement>(NAV_LAYOUT_SELECTOR));
    if (layouts.length === 0) {
        return;
    }

    const state = getState();
    state.cleanup?.();

    const navEntries = await fetchDocsNav();
    const titleByPath = new Map<string, string>(
        navEntries.map((entry) => [normalizeDocsPath(entry.path), entry.title])
    );

    const tree = buildNavTree(navEntries);
    const currentPath = normalizeDocsPath(window.location.pathname);

    const cleanups: Array<() => void> = [];

    for (const layout of layouts) {
        const sidebar = layout.querySelector<HTMLElement>('[data-docs-sidebar]');
        const navRoot = layout.querySelector<HTMLElement>('[data-docs-nav]');
        const breadcrumb = layout.querySelector<HTMLElement>('[data-docs-breadcrumb]');
        const toolbar = layout.querySelector<HTMLElement>('[data-docs-toolbar]');
        const tocPanel = layout.querySelector<HTMLElement>('[data-docs-toc]');
        const tocRoot = layout.querySelector<HTMLElement>('[data-docs-toc-nav]');
        const article = layout.querySelector<HTMLElement>('[data-docs-article]')
            ?? layout.querySelector<HTMLElement>('.docs-article');

        let hasNav = false;
        if (navRoot && sidebar && navEntries.length > 0) {
            const list = renderNavList(tree.children, currentPath);
            navRoot.innerHTML = '';
            navRoot.appendChild(list);
            navRoot.hidden = false;
            sidebar.hidden = false;
            hasNav = true;
        } else if (sidebar) {
            sidebar.hidden = true;
        }

        if (breadcrumb) {
            renderBreadcrumb(breadcrumb, titleByPath, currentPath);
        }
        const breadcrumbVisible = Boolean(breadcrumb && !breadcrumb.hidden);

        let hasToc = false;
        if (tocPanel && tocRoot && article) {
            const { headings, links } = renderToc(tocRoot, article);
            hasToc = headings.length > 0;
            tocPanel.hidden = !hasToc;
            if (hasToc) {
                cleanups.push(setupScrollSpy(headings, links));
            }
        } else if (tocPanel) {
            tocPanel.hidden = true;
        }

        if (toolbar) {
            toolbar.hidden = !(hasNav || hasToc || breadcrumbVisible);
        }

        if (hasNav || hasToc) {
            layout.dataset.contentNavReady = 'true';
        } else {
            layout.dataset.contentNavReady = 'false';
        }

        cleanups.push(setupPanelToggles(layout, hasNav, hasToc));
    }

    state.cleanup = () => {
        for (const fn of cleanups) {
            fn();
        }
    };
}

void initContentNav();
window.addEventListener('webstir:client-nav', () => {
    void initContentNav();
});
