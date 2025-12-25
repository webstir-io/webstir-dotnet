export type DrawerController = {
  open: () => void;
  close: () => void;
  toggle: () => void;
  isOpen: () => boolean;
  syncOverlayOffset: () => void;
  destroy: () => void;
};

type DrawerOverlayOptions = {
  headerSelector?: string;
  target?: HTMLElement;
  varName?: string;
};

type DrawerOptions = {
  root: HTMLElement;
  openAttribute?: string | null;
  openClass?: string | null;
  bodyClass?: string | null;
  overlay?: DrawerOverlayOptions;
  isActive?: () => boolean;
  closeOnEscape?: boolean;
  closeOnOutside?: boolean;
  closeSelectors?: string[];
  trapFocus?: boolean;
  onOpen?: () => void;
  onClose?: () => void;
};

function getFocusable(root: HTMLElement): HTMLElement[] {
  const selectors = [
    'button:not([disabled])',
    'a[href]',
    'input:not([disabled])',
    'textarea:not([disabled])',
    '[tabindex]:not([tabindex="-1"])'
  ].join(',');
  return Array.from(root.querySelectorAll<HTMLElement>(selectors))
    .filter((el) => !el.hasAttribute('hidden') && el.offsetParent !== null);
}

export function createDrawer(options: DrawerOptions): DrawerController {
  const openAttribute = options.openAttribute === undefined ? 'data-open' : options.openAttribute;
  const openClass = options.openClass ?? null;
  const bodyClass = options.bodyClass ?? null;
  const overlayVar = options.overlay?.varName ?? '--ws-drawer-top';
  const overlayTarget = options.overlay?.target ?? options.root;
  const headerSelector = options.overlay?.headerSelector ?? '.app-header';
  const isActive = options.isActive ?? (() => true);
  const closeSelectors = options.closeSelectors?.filter(Boolean).join(',') ?? '';

  let open = false;

  const syncOverlayOffset = () => {
    if (!options.overlay) {
      return;
    }

    const header = document.querySelector<HTMLElement>(headerSelector);
    if (!header) {
      return;
    }

    const rect = header.getBoundingClientRect();
    const top = Math.max(0, Math.round(rect.bottom));
    overlayTarget.style.setProperty(overlayVar, `${top}px`);
  };

  const setOpen = (next: boolean) => {
    if (open === next) {
      return;
    }

    open = next;

    if (openAttribute) {
      if (open) {
        options.root.setAttribute(openAttribute, 'true');
      } else {
        options.root.removeAttribute(openAttribute);
      }
    }

    if (openClass) {
      options.root.classList.toggle(openClass, open);
    }

    if (bodyClass) {
      document.body.classList.toggle(bodyClass, open);
    }

    if (open) {
      options.onOpen?.();
      syncOverlayOffset();
    } else {
      options.onClose?.();
    }
  };

  const handleKeydown = (event: KeyboardEvent) => {
    if (!open || !isActive()) {
      return;
    }

    if (options.closeOnEscape && event.key === 'Escape') {
      event.preventDefault();
      setOpen(false);
      return;
    }

    if (options.trapFocus && event.key === 'Tab') {
      const active = document.activeElement;
      if (!(active instanceof HTMLElement) || !options.root.contains(active)) {
        return;
      }

      const focusable = getFocusable(options.root);
      if (focusable.length === 0) {
        return;
      }

      const currentIndex = focusable.indexOf(active);
      const nextIndex = event.shiftKey
        ? (currentIndex <= 0 ? focusable.length - 1 : currentIndex - 1)
        : (currentIndex === -1 || currentIndex >= focusable.length - 1 ? 0 : currentIndex + 1);

      event.preventDefault();
      focusable[nextIndex]?.focus();
    }
  };

  const handleClick = (event: MouseEvent) => {
    if (!open || !isActive()) {
      return;
    }

    const target = event.target;
    if (!(target instanceof Element)) {
      return;
    }

    if (options.closeOnOutside && !options.root.contains(target)) {
      setOpen(false);
      return;
    }

    if (closeSelectors && target.closest(closeSelectors)) {
      setOpen(false);
    }
  };

  document.addEventListener('keydown', handleKeydown);
  document.addEventListener('click', handleClick, true);

  return {
    open: () => {
      if (!isActive()) {
        return;
      }
      setOpen(true);
    },
    close: () => {
      setOpen(false);
    },
    toggle: () => {
      if (open) {
        setOpen(false);
        return;
      }
      if (!isActive()) {
        return;
      }
      setOpen(true);
    },
    isOpen: () => open,
    syncOverlayOffset,
    destroy: () => {
      document.removeEventListener('keydown', handleKeydown);
      document.removeEventListener('click', handleClick, true);
    }
  };
}
