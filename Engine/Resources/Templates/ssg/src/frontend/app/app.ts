import './scripts/components/menu.js';

// Global app initialization

type HotAsset = {
  type: 'js' | 'css';
  url: string;
  relativePath: string;
};

type HotModuleContext = {
  changedFile: string | null;
  modules: ReadonlyArray<HotAsset>;
  styles: ReadonlyArray<HotAsset>;
  cacheBuster: string;
  timestamp: number;
  asset?: HotAsset;
  previousExports?: unknown;
};

type HotModuleHandlers = {
  accept?: (moduleExports: unknown, context: HotModuleContext) => boolean | Promise<boolean>;
  dispose?: (context: HotModuleContext) => void | Promise<void>;
};

type HotModuleRecord = HotModuleHandlers & {
  currentExports?: unknown;
};

declare global {
  interface Window {
    __webstirEventSource?: EventSource;
    __webstirSetDevStatus?: (status: string, message?: string) => void;
    __webstirOnHmrFallback?: (info: { reason?: string; payload?: unknown; details?: unknown }) => void;
    __webstirRegisterHotModule?: (moduleId: string, handlers: HotModuleHandlers) => void;
    __webstirDispose?: (asset: HotAsset | undefined, context: HotModuleContext) => boolean | Promise<boolean>;
    __webstirAccept?: (moduleExports: unknown, context: HotModuleContext) => boolean | Promise<boolean>;
  }
}

const hotModuleRegistry = new Map<string, HotModuleRecord>();

function ensureRecord(moduleId: string): HotModuleRecord {
  const existing = hotModuleRegistry.get(moduleId);
  if (existing) {
    return existing;
  }

  const created: HotModuleRecord = {};
  hotModuleRegistry.set(moduleId, created);
  return created;
}

function normalizeModuleId(candidate?: string | null): string | null {
  if (!candidate) {
    return null;
  }

  try {
    const url = new URL(candidate, window.location.origin);
    return url.pathname;
  } catch {
    const index = candidate.indexOf('?');
    return index === -1 ? candidate : candidate.slice(0, index);
  }
}

function isPromise<T = unknown>(value: unknown): value is Promise<T> {
  return !!value && typeof (value as PromiseLike<T>).then === 'function';
}

function withHistoryContext(context: HotModuleContext, record: HotModuleRecord): HotModuleContext {
  if (!record.currentExports) {
    return context;
  }

  return {
    ...context,
    previousExports: record.currentExports
  };
}

async function evaluateHandlerResult(result: unknown): Promise<boolean> {
  if (isPromise(result)) {
    const resolved = await result;
    return resolved !== false;
  }

  return result !== false;
}

// Lazy-load error handler on first error
let errorHandlerLoaded = false;

async function loadErrorHandler() {
  if (errorHandlerLoaded) return;
  errorHandlerLoaded = true;

  try {
    const { install } = await import('./error.js');
    install();
  } catch (e) {
    console.error('Failed to load error handler:', e);
  }
}

export function registerHotModule(moduleId: string, handlers: HotModuleHandlers): void {
  const normalized = normalizeModuleId(moduleId);
  if (!normalized) {
    return;
  }

  const record = ensureRecord(normalized);
  record.accept = handlers.accept;
  record.dispose = handlers.dispose;
  hotModuleRegistry.set(normalized, record);
}

window.__webstirRegisterHotModule = registerHotModule;

window.__webstirDispose = async (asset, context) => {
  const moduleId = normalizeModuleId(asset?.url ?? asset?.relativePath);
  if (!moduleId) {
    return true;
  }

  const record = hotModuleRegistry.get(moduleId);
  if (!record) {
    return true;
  }

  if (!record.dispose) {
    return true;
  }

  const contextWithHistory = withHistoryContext(context, record);

  try {
    const result = record.dispose(contextWithHistory);
    if (isPromise(result)) {
      await result;
    }
    return true;
  } catch (error) {
    console.error(`[webstir-hmr] Dispose handler failed for ${moduleId}.`, error);
    return false;
  }
};

window.__webstirAccept = async (moduleExports, context) => {
  const moduleId = normalizeModuleId(context?.asset?.url ?? context?.asset?.relativePath);
  if (!moduleId) {
    return true;
  }

  const record = ensureRecord(moduleId);
  const contextWithHistory = withHistoryContext(context, record);

  let accepted = true;

  if (record.accept) {
    try {
      accepted = await evaluateHandlerResult(record.accept(moduleExports, contextWithHistory));
    } catch (error) {
      console.error(`[webstir-hmr] Accept handler failed for ${moduleId}.`, error);
      accepted = false;
    }
  }

  if (accepted) {
    record.currentExports = moduleExports;
  }

  hotModuleRegistry.set(moduleId, record);
  return accepted;
};

// Set up error listeners that will dynamically import the error handler
window.addEventListener('error', async () => {
  await loadErrorHandler();
  // The installed handler will catch subsequent errors
});

window.addEventListener('unhandledrejection', async () => {
  await loadErrorHandler();
  // The installed handler will catch subsequent rejections
});

// Export for use by pages if needed
export { loadErrorHandler };
