import { createDrawer } from './drawer.js';

const menu = document.querySelector<HTMLElement>('[data-app-menu]');
const toggle = menu?.querySelector<HTMLButtonElement>('.app-menu__toggle');

function resolveBackdrop(root: HTMLElement | null): HTMLElement | null {
  if (!root) {
    return null;
  }

  const existing = root.querySelector<HTMLElement>('.ws-drawer-backdrop')
    ?? document.querySelector<HTMLElement>('[data-drawer="menu"]');
  if (existing) {
    return existing;
  }

  const created = document.createElement('div');
  created.className = 'ws-drawer-backdrop';
  created.setAttribute('data-drawer', 'menu');
  created.setAttribute('data-drawer-close', '');
  created.setAttribute('aria-hidden', 'true');
  document.body.appendChild(created);
  return created;
}

const backdrop = resolveBackdrop(menu);

if (menu && toggle) {
  const mobileQuery = window.matchMedia('(max-width: 40rem)');

  const drawer = createDrawer({
    root: menu,
    openAttribute: null,
    openClass: 'is-open',
    bodyClass: 'webstir-menu-open',
    overlay: {
      headerSelector: '.app-header',
      target: backdrop ?? document.body,
      varName: '--ws-drawer-top'
    },
    isActive: () => mobileQuery.matches,
    closeOnEscape: true,
    closeOnOutside: true,
    closeSelectors: ['.app-nav a', '.app-nav button:not([data-docs-folder])', '[data-drawer-close]'],
    onOpen: () => toggle.setAttribute('aria-expanded', 'true'),
    onClose: () => toggle.setAttribute('aria-expanded', 'false')
  });

  const syncMode = () => {
    if (!mobileQuery.matches) {
      drawer.close();
      return;
    }

    drawer.close();
    drawer.syncOverlayOffset();
  };

  syncMode();
  mobileQuery.addEventListener('change', syncMode);

  toggle.addEventListener('click', () => {
    if (!mobileQuery.matches) {
      return;
    }

    drawer.toggle();
  });

  window.addEventListener('resize', () => {
    if (drawer.isOpen() && mobileQuery.matches) {
      drawer.syncOverlayOffset();
    }
  });
}
