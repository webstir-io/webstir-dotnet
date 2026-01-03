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
};

const STATE_KEY = '__webstirContentNavState';
const NAV_URL = '/docs-nav.json';
const NAV_LAYOUT_SELECTOR = '[data-content-nav="true"]';
const APP_NAV_SELECTOR = '.app-nav';
const APP_NAV_DOCS_SELECTOR = '[data-docs-nav-menu]';

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

function clearAppMenuDocsNav(): void {
    const appNav = document.querySelector<HTMLElement>(APP_NAV_SELECTOR);
    const existing = appNav?.querySelector<HTMLElement>(APP_NAV_DOCS_SELECTOR);
    existing?.remove();
    if (document.body.dataset.docsNavMenu) {
        delete document.body.dataset.docsNavMenu;
    }
}

function renderAppMenuDocsNav(
    tree: NavNode,
    currentPath: string,
    titleByPath: ReadonlyMap<string, string>
): void {
    const appNav = document.querySelector<HTMLElement>(APP_NAV_SELECTOR);
    if (!appNav) {
        return;
    }

    const section = document.createElement('div');
    section.className = 'app-nav__docs';
    section.dataset.docsNavMenu = 'true';

    const title = document.createElement('span');
    title.className = 'app-nav__docs-title';
    title.textContent = titleByPath.get('/docs/') ?? 'Documentation';

    const list = renderNavList(tree.children, currentPath);
    section.appendChild(title);
    section.appendChild(list);

    appNav.appendChild(section);
    document.body.dataset.docsNavMenu = 'true';
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

function toTitleCase(value: string): string {
    return value
        .split(/\s+/)
        .filter((part) => part.length > 0)
        .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
        .join(' ');
}

async function initContentNav(): Promise<void> {
    const layouts = Array.from(document.querySelectorAll<HTMLElement>(NAV_LAYOUT_SELECTOR));
    clearAppMenuDocsNav();
    if (layouts.length === 0) {
        return;
    }

    const navEntries = await fetchDocsNav();
    const titleByPath = new Map<string, string>(
        navEntries.map((entry) => [normalizeDocsPath(entry.path), entry.title])
    );

    const tree = buildNavTree(navEntries);
    const currentPath = normalizeDocsPath(window.location.pathname);
    if (navEntries.length > 0) {
        renderAppMenuDocsNav(tree, currentPath, titleByPath);
    }

    for (const layout of layouts) {
        const sidebar = layout.querySelector<HTMLElement>('[data-docs-sidebar]');
        const navRoot = layout.querySelector<HTMLElement>('[data-docs-nav]');
        const breadcrumb = layout.querySelector<HTMLElement>('[data-docs-breadcrumb]');
        const toolbar = layout.querySelector<HTMLElement>('[data-docs-toolbar]');

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

        if (toolbar) {
            toolbar.hidden = !(hasNav || breadcrumbVisible);
        }

        if (hasNav) {
            layout.dataset.contentNavReady = 'true';
        } else {
            layout.dataset.contentNavReady = 'false';
        }
    }
}

void initContentNav();
window.addEventListener('webstir:client-nav', () => {
    void initContentNav();
});
