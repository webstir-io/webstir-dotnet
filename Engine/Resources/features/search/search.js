const STATE_KEY = '__webstirSearchUiV1';

function getState() {
    const w = window;
    if (w[STATE_KEY]) {
        return w[STATE_KEY];
    }

    const state = {
        entries: null,
        entriesPromise: null,
        open: false,
        scope: 'all'
    };

    w[STATE_KEY] = state;
    return state;
}

const state = getState();

function isTypingTarget(target) {
    return target instanceof HTMLElement
        && (target.isContentEditable
            || target.tagName === 'INPUT'
            || target.tagName === 'TEXTAREA'
            || target.tagName === 'SELECT');
}

function escapeHtml(value) {
    return String(value)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

function stripAndNormalize(value) {
    return String(value).toLowerCase().replace(/\\s+/g, ' ').trim();
}

function normalizeKind(kind) {
    const k = typeof kind === 'string' ? kind.toLowerCase().trim() : '';
    return k == 'page' ? 'page' : 'docs';
}

function computeScore(entry, query) {
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

    if (entry.kind == 'docs') {
        score += 1;
    }

    return score;
}

async function ensureIndexLoaded() {
    if (state.entries !== null) {
        return state.entries;
    }

    state.entriesPromise ??= loadIndex();
    state.entries = await state.entriesPromise;
    return state.entries;
}

async function loadIndex() {
    try {
        const response = await fetch('/search.json', { headers: { Accept: 'application/json' } });
        if (!response.ok) {
            return [];
        }

        const data = await response.json();
        if (!Array.isArray(data)) {
            return [];
        }

        return data
            .filter((entry) => entry && typeof entry.path === 'string' && typeof entry.title === 'string')
            .map((entry) => {
                const kind = normalizeKind(entry.kind);
                const headings = Array.isArray(entry.headings) ? entry.headings.filter((h) => typeof h === 'string') : [];
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

function getSearchTrigger() {
    return document.querySelector('[data-webstir-search-open]');
}

function ensureUi() {
    let root = document.getElementById('webstir-search');
    if (!root) {
        root = document.createElement('div');
        root.id = 'webstir-search';
        root.innerHTML = [
            '<div class="webstir-search__backdrop" data-webstir-search-close></div>',
            '<div class="webstir-search__panel" role="dialog" aria-modal="true" aria-label="Search">',
            '  <div class="webstir-search__header">',
            '    <div class="webstir-search__field">',
            '      <span class="webstir-search__icon" aria-hidden="true">',
            '        <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">',
            '          <circle cx="11" cy="11" r="7"></circle>',
            '          <path d="M20 20l-3.5-3.5"></path>',
            '        </svg>',
            '      </span>',
            '      <input class="webstir-search__input" type="search" placeholder="Search..." autocomplete="off" spellcheck="false" autocapitalize="none" />',
            '      <button type="button" class="webstir-search__close" data-webstir-search-close aria-label="Close search">Esc</button>',
            '    </div>',
            '  </div>',
            '  <div class="webstir-search__scopes" hidden>',
            '    <button type="button" data-scope="all" aria-pressed="true">All</button>',
            '    <button type="button" data-scope="docs" aria-pressed="false">Docs</button>',
            '    <button type="button" data-scope="page" aria-pressed="false">Pages</button>',
            '  </div>',
            '  <div class="webstir-search__body">',
            '    <div class="webstir-search__hint">Type at least 2 characters.</div>',
            '    <ul class="webstir-search__results" hidden></ul>',
            '  </div>',
            '</div>'
        ].join('\n');
        document.body.appendChild(root);
    }

    const styleMode = document.documentElement.getAttribute('data-webstir-search-styles');
    const shouldInjectStyles = !styleMode || styleMode === 'inline';

    if (shouldInjectStyles && !document.getElementById('webstir-search-style')) {
        const style = document.createElement('style');
        style.id = 'webstir-search-style';
        style.textContent = `
#webstir-search {
  position: fixed;
  inset: 0;
  z-index: 9999;
  opacity: 0;
  pointer-events: none;
  transition: opacity 140ms ease;
}
#webstir-search[data-open="true"] { opacity: 1; pointer-events: auto; }
body.webstir-search-open { overflow: hidden; }

.webstir-search__trigger {
  display: inline-flex;
  align-items: center;
  gap: 10px;
  padding: 8px 10px;
  border-radius: 10px;
  border: 1px solid rgba(148, 163, 184, 0.18);
  background: rgba(255,255,255,0.02);
  color: inherit;
  cursor: pointer;
  font-weight: 650;
  transition: border-color 140ms ease, background-color 140ms ease, transform 140ms ease;
}
.webstir-search__trigger:hover { background: rgba(255,255,255,0.04); border-color: rgba(148, 163, 184, 0.28); }
.webstir-search__trigger:focus-visible { outline: 3px solid rgba(37, 99, 235, 0.35); outline-offset: 2px; }
.webstir-search__trigger[aria-expanded="true"] { border-color: rgba(37, 99, 235, 0.45); }
.webstir-search__trigger-icon { display: inline-flex; opacity: 0.9; }
.webstir-search__trigger-text { opacity: 0.9; }
.webstir-search__trigger-hint {
  margin-left: 6px;
  padding: 3px 8px;
  border-radius: 999px;
  border: 1px solid rgba(148, 163, 184, 0.22);
  color: rgba(229,231,235,0.85);
  font-size: 12px;
  font-weight: 750;
}
@media (max-width: 700px) {
  .webstir-search__trigger-text { display: none; }
  .webstir-search__trigger-hint { display: none; }
}

.webstir-search__group {
  margin-top: 10px;
  color: rgba(229,231,235,0.7);
  font-size: 12px;
  font-weight: 800;
  letter-spacing: 0.02em;
  text-transform: uppercase;
}
.webstir-search__backdrop {
  position: absolute;
  inset: 0;
  background: rgba(0,0,0,0.55);
  backdrop-filter: blur(8px);
}
.webstir-search__panel {
  position: relative;
  margin: 10vh auto 0;
  width: min(920px, calc(100vw - 32px));
  border-radius: 14px;
  border: 1px solid rgba(148, 163, 184, 0.18);
  background: rgba(15, 23, 42, 0.92);
  color: #e5e7eb;
  overflow: hidden;
  box-shadow: 0 30px 80px rgba(0,0,0,0.55);
  transform: translateY(-10px) scale(0.985);
  opacity: 0;
  transition: transform 160ms ease, opacity 160ms ease;
  max-height: min(70vh, 720px);
  display: flex;
  flex-direction: column;
}
#webstir-search[data-open="true"] .webstir-search__panel { transform: translateY(0) scale(1); opacity: 1; }
@media (prefers-reduced-motion: reduce) {
  #webstir-search { transition: none; }
  .webstir-search__panel { transition: none; transform: none; }
  .webstir-search__trigger { transition: none; }
}

.webstir-search__header { padding: 14px; border-bottom: 1px solid rgba(148, 163, 184, 0.14); }
.webstir-search__field {
  display: grid;
  grid-template-columns: auto 1fr auto;
  gap: 10px;
  align-items: center;
  padding: 10px 12px;
  border-radius: 12px;
  border: 1px solid rgba(148, 163, 184, 0.18);
  background: rgba(2, 6, 23, 0.38);
}
.webstir-search__icon { display: inline-flex; opacity: 0.9; }
.webstir-search__input {
  width: 100%;
  padding: 6px 2px;
  border: 0;
  background: transparent;
  color: inherit;
  outline: none;
  font-size: 16px;
  min-width: 0;
}
.webstir-search__field:focus-within { outline: 3px solid rgba(37, 99, 235, 0.45); outline-offset: 2px; }
.webstir-search__close {
  padding: 6px 10px;
  border-radius: 10px;
  border: 1px solid rgba(148, 163, 184, 0.18);
  background: transparent;
  color: rgba(229,231,235,0.85);
  font-size: 12px;
  font-weight: 800;
  letter-spacing: 0.02em;
  text-transform: uppercase;
  cursor: pointer;
}
.webstir-search__close:hover { background: rgba(255,255,255,0.06); }
.webstir-search__scopes { display: flex; gap: 8px; padding: 0 14px 12px; }
.webstir-search__scopes button { padding: 6px 10px; border-radius: 999px; border: 1px solid rgba(148, 163, 184, 0.18); background: transparent; color: inherit; cursor: pointer; font-weight: 750; font-size: 13px; }
.webstir-search__scopes button[aria-pressed="true"] { background: rgba(37,99,235,0.22); border-color: rgba(37,99,235,0.4); }
.webstir-search__body { padding: 14px; overflow: auto; }
.webstir-search__hint { color: rgba(229,231,235,0.75); }
.webstir-search__results { list-style: none; margin: 12px 0 0; padding: 0; display: grid; gap: 8px; }
.webstir-search__results a { display: block; padding: 12px 12px; border-radius: 12px; border: 1px solid rgba(148, 163, 184, 0.16); background: rgba(255,255,255,0.03); color: inherit; text-decoration: none; }
.webstir-search__results a:hover { border-color: rgba(37,99,235,0.35); background: rgba(255,255,255,0.045); }
.webstir-search__results a:focus-visible { outline: 3px solid rgba(37, 99, 235, 0.45); outline-offset: 2px; }
.webstir-search__results strong { display: block; font-weight: 850; margin-bottom: 4px; }
.webstir-search__results span { display: block; color: rgba(229,231,235,0.75); font-size: 0.95rem; line-height: 1.35; }
        `.trim();
        document.head.appendChild(style);
    }

    const nav = document.querySelector('.app-nav');
    if (nav) {
        const isMac = typeof navigator !== 'undefined' && /mac/i.test(navigator.platform ?? '');
        const hint = isMac ? 'Cmd K' : 'Ctrl K';

        const triggerContent = [
            '<span class="webstir-search__trigger-icon" aria-hidden="true">',
            '  <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">',
            '    <circle cx="11" cy="11" r="7"></circle>',
            '    <path d="M20 20l-3.5-3.5"></path>',
            '  </svg>',
            '</span>',
            '<span class="webstir-search__trigger-text">Search</span>',
            `<span class="webstir-search__trigger-hint" aria-hidden="true">${escapeHtml(hint)}</span>`
        ].join('');

        const existingTrigger = nav.querySelector('[data-webstir-search-open]');
        if (existingTrigger instanceof HTMLElement) {
            existingTrigger.classList.add('webstir-search__trigger');
            existingTrigger.setAttribute('data-webstir-search-open', '');
            existingTrigger.setAttribute('aria-label', 'Search');
            existingTrigger.setAttribute('aria-haspopup', 'dialog');
            if (!existingTrigger.hasAttribute('aria-expanded')) {
                existingTrigger.setAttribute('aria-expanded', 'false');
            }
            existingTrigger.removeAttribute('hidden');
            if (existingTrigger instanceof HTMLButtonElement) {
                existingTrigger.type = 'button';
            }
            if (!existingTrigger.innerHTML.trim()) {
                existingTrigger.innerHTML = triggerContent;
            }
        } else {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'webstir-search__trigger';
            button.setAttribute('data-webstir-search-open', '');
            button.setAttribute('aria-label', 'Search');
            button.setAttribute('aria-haspopup', 'dialog');
            button.setAttribute('aria-expanded', 'false');
            button.innerHTML = triggerContent;
            nav.appendChild(button);
        }
    }

    return root;
}

function setScope(scope) {
    state.scope = scope;
    const root = document.getElementById('webstir-search');
    if (!root) {
        return;
    }

    root.querySelectorAll('.webstir-search__scopes button[data-scope]').forEach((button) => {
        const s = button.getAttribute('data-scope');
        button.setAttribute('aria-pressed', s == scope ? 'true' : 'false');
    });
}

function openSearch(options) {
    const root = ensureUi();
    root.setAttribute('data-open', 'true');
    state.open = true;
    document.body.classList.add('webstir-search-open');

    const trigger = getSearchTrigger();
    if (trigger instanceof HTMLElement) {
        trigger.setAttribute('aria-expanded', 'true');
    }

    const input = root.querySelector('.webstir-search__input');
    if (input) {
        if (options && typeof options.initialQuery === 'string') {
            input.value = options.initialQuery;
            input.focus();
        } else {
        input.focus();
        input.select();
        }
    }

    void refreshResults();
}

function closeSearch() {
    const root = document.getElementById('webstir-search');
    if (!root) {
        return;
    }

    root.removeAttribute('data-open');
    state.open = false;
    document.body.classList.remove('webstir-search-open');

    const trigger = getSearchTrigger();
    if (trigger instanceof HTMLElement) {
        trigger.setAttribute('aria-expanded', 'false');
    }
}

async function refreshResults() {
    const root = document.getElementById('webstir-search');
    if (!root) {
        return;
    }

    const input = root.querySelector('.webstir-search__input');
    const hint = root.querySelector('.webstir-search__hint');
    const results = root.querySelector('.webstir-search__results');
    const scopes = root.querySelector('.webstir-search__scopes');
    if (!input || !results || !hint || !scopes) {
        return;
    }

    const entries = await ensureIndexLoaded();
    const kinds = new Set(entries.map((e) => e.kind));
    if (kinds.size <= 1) {
        scopes.setAttribute('hidden', '');
    } else {
        scopes.removeAttribute('hidden');
    }

    const query = stripAndNormalize(input.value);
    if (!query || query.length < 2) {
        results.setAttribute('hidden', '');
        results.innerHTML = '';
        hint.textContent = entries.length > 0 ? 'Type to search.' : 'Search index not found yet.';
        return;
    }

    const matches = entries
        .filter((entry) => (state.scope == 'all' ? true : entry.kind == state.scope))
        .filter((entry) => entry.haystack.includes(query))
        .map((entry) => ({ entry, score: computeScore(entry, query) }))
        .sort((a, b) => {
            if (a.score != b.score) {
                return b.score - a.score;
            }
            return String(a.entry.title).localeCompare(String(b.entry.title));
        })
        .map((item) => item.entry);

    if (matches.length == 0) {
        results.removeAttribute('hidden');
        results.innerHTML = '<li><span>No matches.</span></li>';
        hint.textContent = '';
        return;
    }

    hint.textContent = '';
    results.removeAttribute('hidden');

    const renderEntry = (entry) => {
        const title = escapeHtml(entry.title);
        const excerpt = escapeHtml(entry.excerpt);
        const href = escapeHtml(entry.path);
        return `<li><a href="${href}"><strong>${title}</strong><span>${excerpt}</span></a></li>`;
    };

    const shouldGroup = state.scope == 'all' && kinds.size > 1;
    if (!shouldGroup) {
        results.innerHTML = matches.slice(0, 12).map(renderEntry).join('');
        return;
    }

    const docs = matches.filter((m) => m.kind == 'docs').slice(0, 6);
    const pages = matches.filter((m) => m.kind == 'page').slice(0, 6);

    const sections = [];
    if (docs.length > 0) {
        sections.push('<li class="webstir-search__group">Docs</li>');
        sections.push(...docs.map(renderEntry));
    }
    if (pages.length > 0) {
        sections.push('<li class="webstir-search__group">Pages</li>');
        sections.push(...pages.map(renderEntry));
    }

    results.innerHTML = sections.join('');
}

function boot() {
    const root = ensureUi();
    const input = root.querySelector('.webstir-search__input');
    const closeButtons = root.querySelectorAll('[data-webstir-search-close]');

    closeButtons.forEach((button) => {
        button.addEventListener('click', () => closeSearch());
    });

    root.addEventListener('click', (event) => {
        const target = event.target;
        if (target instanceof Element) {
            const resultLink = target.closest('.webstir-search__results a');
            if (resultLink) {
                closeSearch();
                return;
            }
        }
        if (target && target.matches && target.matches('.webstir-search__backdrop')) {
            closeSearch();
        }
    });

    root.querySelectorAll('.webstir-search__scopes button[data-scope]').forEach((button) => {
        button.addEventListener('click', () => {
            const scope = button.getAttribute('data-scope') || 'all';
            setScope(scope);
            void refreshResults();
        });
    });

    if (input) {
        input.addEventListener('input', () => void refreshResults());
        input.addEventListener('keydown', (event) => {
            if (event.key == 'Escape') {
                event.preventDefault();
                closeSearch();
            }
        });
    }

    document.addEventListener('click', (event) => {
        const target = event.target;
        if (!target || !target.matches) {
            return;
        }

        if (target.matches('[data-webstir-search-open]')) {
            event.preventDefault();
            openSearch();
        }
    });

    document.addEventListener('keydown', (event) => {
        if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() == 'k') {
            event.preventDefault();
            openSearch();
            return;
        }
        if (event.key == 'Escape' && state.open) {
            event.preventDefault();
            closeSearch();
        }

        if (!state.open && !event.ctrlKey && !event.metaKey && !event.altKey && event.key.length === 1) {
            if (event.key.trim().length === 0) {
                return;
            }
            if (isTypingTarget(event.target)) {
                return;
            }
            openSearch({ initialQuery: event.key });
        }
    });

    window.addEventListener('webstir:client-nav', () => {
        if (state.open) {
            closeSearch();
        }
    });
}

boot();
