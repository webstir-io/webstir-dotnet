type DocsNavEntry = {
  path: string;
  title: string;
};

const indexSelector = '[data-docs-index]';
const indexRoot = document.querySelector<HTMLElement>(indexSelector);

if (indexRoot) {
  void populateDocsIndex(indexRoot);
}

window.addEventListener('webstir:client-nav', () => {
  const nextRoot = document.querySelector<HTMLElement>(indexSelector);
  if (nextRoot) {
    void populateDocsIndex(nextRoot);
  }
});

async function populateDocsIndex(root: HTMLElement): Promise<void> {
  const entries = await fetchDocsNav();
  root.innerHTML = '';

  if (entries.length === 0) {
    const empty = document.createElement('p');
    empty.className = 'docs-index__empty';
    empty.textContent = 'No docs yet. Add Markdown files to src/frontend/content/.';
    root.appendChild(empty);
    return;
  }

  const list = document.createElement('ol');
  list.className = 'docs-index__list';

  for (const entry of entries) {
    const item = document.createElement('li');
    item.className = 'docs-index__item';

    const link = document.createElement('a');
    link.className = 'docs-index__link';
    link.href = entry.path;
    link.textContent = entry.title;

    item.appendChild(link);
    list.appendChild(item);
  }

  root.appendChild(list);
}

async function fetchDocsNav(): Promise<DocsNavEntry[]> {
  try {
    const response = await fetch('/docs-nav.json');
    if (!response.ok) {
      return [];
    }
    const payload = await response.json();
    if (!Array.isArray(payload)) {
      return [];
    }
    return payload
      .filter((entry): entry is DocsNavEntry => Boolean(entry && entry.path && entry.title))
      .map((entry) => ({
        path: String(entry.path),
        title: String(entry.title)
      }));
  } catch {
    return [];
  }
}
