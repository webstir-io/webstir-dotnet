type DocsNavEntry = {
  path: string;
  title: string;
  order?: number;
};

type FolderNode = {
  kind: 'folder';
  key: string;
  label: string;
  index?: DocsNavEntry;
  children: Map<string, FolderNode>;
  pages: DocsNavEntry[];
};

type DocsUiState = {
  nav?: DocsNavEntry[] | null;
  navPromise?: Promise<DocsNavEntry[] | null>;
  tocObserver?: IntersectionObserver;
};

function getState(): DocsUiState {
  const w = window as unknown as Record<string, unknown>;
  const key = '__webstirDocsUiStateV1';
  const existing = w[key] as DocsUiState | undefined;
  if (existing) {
    return existing;
  }
  const state: DocsUiState = {};
  w[key] = state;
  return state;
}

const state = getState();

function normalizePath(value: string): string {
  const trimmed = value.trim();
  if (!trimmed.startsWith('/')) {
    return `/${trimmed}`;
  }
  return trimmed.endsWith('/') ? trimmed : `${trimmed}/`;
}

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

function toTitleCase(value: string): string {
  return value
    .split(/[-_\\s]+/g)
    .filter(Boolean)
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(' ');
}

function parseDocsSegments(urlPath: string): string[] {
  const normalized = normalizePath(urlPath);
  const parts = normalized.split('/').filter(Boolean);
  if (parts[0] === 'docs') {
    return parts.slice(1);
  }
  return parts;
}

function joinPath(segments: readonly string[]): string {
  return segments.join('/');
}

async function loadJson<T>(path: string): Promise<T | null> {
  try {
    const response = await fetch(path, { headers: { Accept: 'application/json' } });
    if (!response.ok) return null;
    return (await response.json()) as T;
  } catch {
    return null;
  }
}

async function ensureNavLoaded(): Promise<DocsNavEntry[] | null> {
  if (state.nav !== undefined) {
    return state.nav;
  }
  state.navPromise ??= loadJson<DocsNavEntry[]>('/docs-nav.json');
  state.nav = await state.navPromise;
  return state.nav;
}

function loadOpenState(): Record<string, boolean> {
  try {
    const raw = window.localStorage.getItem('webstir.docs.nav.open');
    if (!raw) return {};
    const parsed = JSON.parse(raw) as unknown;
    if (!parsed || typeof parsed !== 'object') return {};
    return parsed as Record<string, boolean>;
  } catch {
    return {};
  }
}

function saveOpenState(map: Record<string, boolean>): void {
  try {
    window.localStorage.setItem('webstir.docs.nav.open', JSON.stringify(map));
  } catch {
    // ignore
  }
}

function buildNavTree(entries: DocsNavEntry[]): { root: FolderNode; folderPaths: ReadonlySet<string> } {
  const root: FolderNode = { kind: 'folder', key: '', label: '', children: new Map(), pages: [] };

  const folderPaths = new Set<string>();
  for (const entry of entries) {
    const segments = parseDocsSegments(entry.path);
    if (segments.length <= 1) continue;
    for (let index = 0; index < segments.length - 1; index += 1) {
      folderPaths.add(joinPath(segments.slice(0, index + 1)));
    }
  }

  for (const entry of entries) {
    const segments = parseDocsSegments(entry.path);
    if (segments.length === 0) continue;

    const first = segments[0];
    if (!first) continue;

    if (segments.length === 1 && !folderPaths.has(first)) {
      root.pages.push(entry);
      continue;
    }

    let cursor = root;
    for (let index = 0; index < segments.length; index += 1) {
      const segment = segments[index]!;
      const isLeaf = index === segments.length - 1;
      const prefixKey = joinPath(segments.slice(0, index + 1));
      const shouldBeFolder = folderPaths.has(prefixKey) || !isLeaf;

      if (shouldBeFolder) {
        let next = cursor.children.get(segment);
        if (!next) {
          next = { kind: 'folder', key: prefixKey, label: toTitleCase(segment), children: new Map(), pages: [] };
          cursor.children.set(segment, next);
        }

        if (isLeaf) {
          next.index = entry;
          next.label = entry.title?.trim() ? entry.title.trim() : next.label;
        }

        cursor = next;
      } else {
        cursor.pages.push(entry);
      }
    }
  }

  return { root, folderPaths };
}

function sortEntries(a: DocsNavEntry, b: DocsNavEntry): number {
  const aOrder = typeof a.order === 'number' ? a.order : Number.POSITIVE_INFINITY;
  const bOrder = typeof b.order === 'number' ? b.order : Number.POSITIVE_INFINITY;
  if (aOrder !== bOrder) return aOrder - bOrder;
  return normalizePath(a.path).localeCompare(normalizePath(b.path));
}

function normalizeEntries(entries: DocsNavEntry[]): DocsNavEntry[] {
  return entries
    .filter((entry) => entry && typeof entry.path === 'string' && typeof entry.title === 'string')
    .map((entry) => ({ ...entry, path: normalizePath(entry.path) }));
}

function renderSidebar(entries: DocsNavEntry[], links: HTMLElement): void {

  const normalizedCurrent = normalizePath(window.location.pathname);
  const safeEntries = normalizeEntries(entries);

  safeEntries.sort(sortEntries);

  const openState = loadOpenState();
  const currentSegments = parseDocsSegments(window.location.pathname);

  const { root } = buildNavTree(safeEntries);

  const renderLink = (entry: DocsNavEntry): string => {
    const href = normalizePath(entry.path);
    const isCurrent = href === normalizedCurrent;
    const currentAttr = isCurrent ? ' aria-current="page"' : '';
    return `<li><a href="${escapeHtml(href)}"${currentAttr}>${escapeHtml(entry.title)}</a></li>`;
  };

  const renderFolder = (node: FolderNode, depth: number): string => {
    const hasChildren = node.children.size > 0 || node.pages.length > 0;
    const hasIndexOnly = Boolean(node.index) && !hasChildren;
    if (hasIndexOnly && node.index) {
      return renderLink(node.index);
    }

    const folderKey = node.key;
    const defaultOpen = depth === 0 || currentSegments.join('/').startsWith(folderKey);
    const expanded = openState[folderKey] ?? defaultOpen;
    const childrenHidden = expanded ? '' : ' hidden';

    const childItems: string[] = [];

    const shouldShowOverview =
      Boolean(node.index)
      && (node.pages.length > 0 || node.children.size > 0)
      && node.index!.title.trim().toLowerCase() === node.label.trim().toLowerCase();

    if (node.index) {
      const entry = shouldShowOverview ? { ...node.index, title: 'Overview' } : node.index;
      childItems.push(renderLink(entry));
    }

    const pages = [...node.pages].sort(sortEntries).map(renderLink);
    childItems.push(...pages);

    const folders = Array.from(node.children.values()).sort((a, b) => a.label.localeCompare(b.label));
    for (const folder of folders) {
      childItems.push(renderFolder(folder, depth + 1));
    }

    const toggleLabel = escapeHtml(node.label);
    const caret = '<span class="docs-tree__caret" aria-hidden="true"></span>';
    const ariaExpanded = expanded ? 'true' : 'false';

    return [
      '<li class="docs-tree">',
      `  <button type="button" class="docs-tree__toggle" data-docs-folder="${escapeHtml(folderKey)}" aria-expanded="${ariaExpanded}">`,
      `    ${caret}<span>${toggleLabel}</span>`,
      '  </button>',
      `  <ul class="docs-tree__children"${childrenHidden}>${childItems.join('')}</ul>`,
      '</li>'
    ].join('');
  };

  const rendered: string[] = [];

  root.pages.sort(sortEntries).forEach((entry) => rendered.push(renderLink(entry)));

  const topFolders = Array.from(root.children.values()).sort((a, b) => a.label.localeCompare(b.label));
  topFolders.forEach((folder) => rendered.push(renderFolder(folder, 0)));

  links.innerHTML = rendered.join('');

  links.querySelectorAll<HTMLButtonElement>('button[data-docs-folder]').forEach((button) => {
    button.addEventListener('click', () => {
      const key = button.getAttribute('data-docs-folder');
      if (!key) return;

      const expanded = button.getAttribute('aria-expanded') === 'true';
      const nextExpanded = !expanded;
      button.setAttribute('aria-expanded', nextExpanded ? 'true' : 'false');

      const parent = button.closest('li');
      const children = parent?.querySelector<HTMLElement>('.docs-tree__children');
      if (children) {
        if (nextExpanded) {
          children.removeAttribute('hidden');
        } else {
          children.setAttribute('hidden', '');
        }
      }

      openState[key] = nextExpanded;
      saveOpenState(openState);
    });
  });
}

function ensureBreadcrumbContainer(): HTMLElement | null {
  const main = document.querySelector<HTMLElement>('.docs-main');
  if (!main) return null;

  let container = main.querySelector<HTMLElement>('.docs-breadcrumb');
  if (!container) {
    container = document.createElement('nav');
    container.className = 'docs-breadcrumb';
    container.setAttribute('aria-label', 'Breadcrumb');
    main.insertBefore(container, main.firstChild);
  }
  return container;
}

function renderBreadcrumbs(entries: DocsNavEntry[]): void {
  const segments = parseDocsSegments(window.location.pathname);
  const container = ensureBreadcrumbContainer();
  if (!container) return;

  const safeEntries = normalizeEntries(entries);
  const entryByPath = new Map<string, DocsNavEntry>();
  safeEntries.forEach((entry) => {
    entryByPath.set(normalizePath(entry.path), entry);
  });

  const { root } = buildNavTree(safeEntries);

  const isRoot = segments.length === 0;
  const crumbs: Array<{ label: string; href?: string; current?: boolean; home?: boolean }> = [
    { label: 'Docs', href: isRoot ? undefined : '/docs/', current: isRoot, home: true }
  ];

  let cursor: FolderNode | undefined = root;
  const prefix: string[] = [];

  segments.forEach((segment, index) => {
    prefix.push(segment);
    const prefixKey = joinPath(prefix);
    const path = normalizePath(`/docs/${prefixKey}`);
    const entry = entryByPath.get(path);

    let label = entry?.title?.trim() ? entry.title.trim() : toTitleCase(segment);
    let href: string | undefined = entry ? path : undefined;

    const next = cursor?.children.get(segment);
    if (next) {
      if (!entry) {
        label = next.label || label;
        if (next.index) {
          href = normalizePath(next.index.path);
        }
      }
      cursor = next;
    }

    const isCurrent = index === segments.length - 1;
    crumbs.push({ label, href: isCurrent ? undefined : href, current: isCurrent });
  });

  const homeIcon = [
    '<span class="docs-breadcrumb__icon" aria-hidden="true">',
    '  <svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor">',
    '    <path d="M11.3 3.4a1 1 0 0 1 1.4 0l8 6.6a1 1 0 0 1-.6 1.8h-1.1V20a1 1 0 0 1-1 1h-4.5a1 1 0 0 1-1-1v-5h-3v5a1 1 0 0 1-1 1H6a1 1 0 0 1-1-1v-8.4H3.9a1 1 0 0 1-.6-1.8l8-6.6Z"></path>',
    '  </svg>',
    '</span>'
  ].join('');

  const items = crumbs.map((crumb) => {
    const label = escapeHtml(crumb.label);
    const currentClass = crumb.current ? ' docs-breadcrumb__label--current' : '';
    const labelHtml = `<span class="docs-breadcrumb__label${currentClass}">${label}</span>`;
    const content = crumb.home ? `${homeIcon}${labelHtml}` : labelHtml;

    if (crumb.href && !crumb.current) {
      const ariaLabel = crumb.home ? ' aria-label="Docs home"' : '';
      return `<li><a class="docs-breadcrumb__item" href="${escapeHtml(crumb.href)}"${ariaLabel}>${content}</a></li>`;
    }
    const currentAttr = crumb.current ? ' aria-current="page"' : '';
    return `<li><span class="docs-breadcrumb__item"${currentAttr}>${content}</span></li>`;
  });

  container.removeAttribute('hidden');
  container.innerHTML = `<ol class="docs-breadcrumb__list">${items.join('')}</ol>`;
}

function slugifyHeading(value: string): string {
  return value
    .trim()
    .toLowerCase()
    .replace(/['"]/g, '')
    .replace(/[^a-z0-9\\s-]/g, '')
    .replace(/\\s+/g, '-')
    .replace(/-+/g, '-');
}

function refreshToc(): void {
  const layout = document.querySelector<HTMLElement>('.docs-layout');
  const article = document.querySelector<HTMLElement>('.docs-article');
  const tocAside = document.querySelector<HTMLElement>('.docs-toc');
  const tocList = document.getElementById('docs-toc');

  if (!layout || !tocAside || !tocList || !article) {
    layout?.classList.remove('has-toc');
    return;
  }

  if (state.tocObserver) {
    state.tocObserver.disconnect();
    state.tocObserver = undefined;
  }

  const headings = Array.from(article.querySelectorAll<HTMLElement>('h2, h3'));
  if (headings.length === 0) {
    tocAside.setAttribute('hidden', '');
    layout.classList.remove('has-toc');
    tocList.innerHTML = '';
    return;
  }

  const used = new Set<string>();
  const ensureId = (heading: HTMLElement): string => {
    const existing = heading.id?.trim();
    if (existing) {
      used.add(existing);
      return existing;
    }

    const base = slugifyHeading(heading.textContent ?? '') || 'section';
    let candidate = base;
    let counter = 2;
    while (used.has(candidate)) {
      candidate = `${base}-${counter}`;
      counter += 1;
    }
    used.add(candidate);
    heading.id = candidate;
    return candidate;
  };

  const items = headings.map((heading) => {
    const id = ensureId(heading);
    const level = heading.tagName.toLowerCase() === 'h3' ? 3 : 2;
    const className = level === 3 ? ' class="docs-toc__sub"' : '';
    return `<li${className}><a href="#${escapeHtml(id)}">${escapeHtml(heading.textContent ?? '')}</a></li>`;
  });

  tocList.innerHTML = items.join('');
  tocAside.removeAttribute('hidden');
  layout.classList.add('has-toc');

  const tocLinks = Array.from(tocList.querySelectorAll<HTMLAnchorElement>('a[href^=\"#\"]'));
  const linkById = new Map<string, HTMLAnchorElement>();
  tocLinks.forEach((anchor) => {
    const raw = anchor.getAttribute('href') ?? '';
    const id = raw.startsWith('#') ? raw.slice(1) : raw;
    if (id) linkById.set(id, anchor);
  });

  tocLinks.forEach((anchor) => {
    anchor.addEventListener('click', (event) => {
      const raw = anchor.getAttribute('href') ?? '';
      const id = raw.startsWith('#') ? raw.slice(1) : raw;
      if (!id) return;
      const target = document.getElementById(id);
      if (!target) return;
      event.preventDefault();
      target.scrollIntoView({ behavior: 'smooth', block: 'start' });
      history.pushState({}, '', `#${id}`);
    });
  });

  const observer = new IntersectionObserver(
    (entries) => {
      const visible = entries
        .filter((entry) => entry.isIntersecting)
        .sort((a, b) => (a.boundingClientRect.top ?? 0) - (b.boundingClientRect.top ?? 0));
      const active = visible[0]?.target as HTMLElement | undefined;
      if (!active?.id) return;

      tocLinks.forEach((link) => link.removeAttribute('aria-current'));
      linkById.get(active.id)?.setAttribute('aria-current', 'true');
    },
    { root: null, rootMargin: '-20% 0px -70% 0px', threshold: [0, 1] }
  );

  headings.forEach((heading) => observer.observe(heading));
  state.tocObserver = observer;
}

async function refresh(): Promise<void> {
  const nav = await ensureNavLoaded();
  if (nav) {
    const sidebarLinks = document.getElementById('docs-links');
    if (sidebarLinks) {
      renderSidebar(nav, sidebarLinks);
    }

    renderBreadcrumbs(nav);
  }
  refreshToc();
}

function boot(): void {
  const w = window as unknown as Record<string, unknown>;
  const key = '__webstirDocsUiBootedV1';
  if (w[key] === true) {
    void refresh();
    return;
  }

  w[key] = true;
  window.addEventListener('webstir:client-nav', () => {
    void refresh();
  });
  void refresh();
}

boot();
