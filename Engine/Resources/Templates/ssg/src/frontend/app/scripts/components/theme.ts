const root = document.documentElement;
const toggle = document.querySelector<HTMLButtonElement>('[data-theme-toggle="true"]');

if (toggle) {
  const storageKey = 'webstir-theme';

  const normalizeTheme = (value: string | null): 'dark' | 'light' | null => {
    if (value === 'dark' || value === 'light') {
      return value;
    }
    return null;
  };

  const getStoredTheme = (): 'dark' | 'light' | null => {
    try {
      return normalizeTheme(localStorage.getItem(storageKey));
    } catch {
      return null;
    }
  };

  const getPreferredTheme = (): 'dark' | 'light' => {
    if (window.matchMedia?.('(prefers-color-scheme: dark)').matches) {
      return 'dark';
    }
    return 'light';
  };

  const applyTheme = (theme: 'dark' | 'light', persist: boolean): void => {
    root.setAttribute('data-theme', theme);
    toggle.setAttribute('aria-pressed', theme === 'dark' ? 'true' : 'false');

    const label = theme === 'dark' ? 'Switch to light theme' : 'Switch to dark theme';
    toggle.setAttribute('aria-label', label);
    toggle.setAttribute('title', label);

    if (persist) {
      try {
        localStorage.setItem(storageKey, theme);
      } catch {
        // Ignore storage errors (private mode, disabled storage).
      }
    }
  };

  const initialTheme =
    getStoredTheme()
    ?? normalizeTheme(root.getAttribute('data-theme'))
    ?? getPreferredTheme();

  applyTheme(initialTheme, false);

  toggle.addEventListener('click', () => {
    const currentTheme = normalizeTheme(root.getAttribute('data-theme')) ?? 'dark';
    const nextTheme = currentTheme === 'dark' ? 'light' : 'dark';
    applyTheme(nextTheme, true);
  });
}
