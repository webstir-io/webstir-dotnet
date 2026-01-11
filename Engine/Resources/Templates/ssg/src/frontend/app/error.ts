// Global client-side error reporter (TypeScript)
let lastSentAt = 0;
let sentCount = 0;
const MAX_PER_SESSION = 20;
const MIN_INTERVAL_MS = 1000;
const DEDUPE_WINDOW_MS = 60_000; // 60s
const recent = new Map<string, number>(); // fingerprint -> timestamp
const BASE_PATH = resolveBasePath();

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

function cid(): string {
  const w = window as any;
  if (!w.__WEBSTIR_CID__) {
    w.__WEBSTIR_CID__ = 'c-' + Math.random().toString(36).slice(2) + Date.now().toString(36);
  }
  return String(w.__WEBSTIR_CID__);
}

type Payload = {
  type: 'error' | 'unhandledrejection';
  message: string;
  stack: string;
  filename: string;
  lineno: number;
  colno: number;
  pageUrl: string;
  userAgent: string;
  timestamp: string;
  correlationId: string;
};

function toPayload(e: ErrorEvent | PromiseRejectionEvent): Payload {
  const isRejection = !!e && e.type === 'unhandledrejection';
  const reason: any = isRejection ? ((e as PromiseRejectionEvent).reason || {}) : {};
  const err: any = (e as ErrorEvent)?.error || reason || {};
  const message = (e as ErrorEvent)?.message || reason.message || err?.message || 'Unknown error';
  const stack = String(err?.stack || reason.stack || '');
  const filename = String((e as ErrorEvent)?.filename || '');
  const lineno = Number((e as ErrorEvent)?.lineno || 0);
  const colno = Number((e as ErrorEvent)?.colno || 0);
  return {
    type: isRejection ? 'unhandledrejection' : 'error',
    message: String(message || ''),
    stack,
    filename,
    lineno,
    colno,
    pageUrl: String(location.href),
    userAgent: String(navigator.userAgent || ''),
    timestamp: new Date().toISOString(),
    correlationId: cid(),
  };
}

// Simple 32-bit FNV-1a
function hash(str: string): string {
  let h = 2166136261 >>> 0;
  for (let i = 0; i < str.length; i++) {
    h ^= str.charCodeAt(i);
    h = (h + (h << 1) + (h << 4) + (h << 7) + (h << 8) + (h << 24)) >>> 0;
  }
  return h.toString(36);
}

function fingerprint(p: Payload): string {
  return [
    p.type || '',
    p.message || '',
    `${p.filename || ''}:${p.lineno || 0}:${p.colno || 0}`,
    hash(p.stack || ''),
  ].join('|');
}

function shouldSend(p: Payload): boolean {
  const now = Date.now();
  if (now - lastSentAt < MIN_INTERVAL_MS) return false;
  if (sentCount >= MAX_PER_SESSION) return false;

  const fp = fingerprint(p);
  const last = recent.get(fp) || 0;

  // prune old entries opportunistically
  if (recent.size > 100) {
    recent.forEach((ts, k) => {
      if (now - ts > DEDUPE_WINDOW_MS) recent.delete(k);
    });
  }

  if (now - last < DEDUPE_WINDOW_MS) return false;
  recent.set(fp, now);
  lastSentAt = now;
  sentCount++;
  return true;
}

export function report(e: ErrorEvent | PromiseRejectionEvent): void {
  try {
    const p = toPayload(e);
    if (!shouldSend(p)) return;
    const payload = JSON.stringify(p);
    if (navigator.sendBeacon) {
      const blob = new Blob([payload], { type: 'application/json' });
      navigator.sendBeacon(withBasePath('/client-errors'), blob);
    } else {
      fetch(withBasePath('/client-errors'), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'X-Correlation-ID': cid() },
        body: payload,
        keepalive: true,
      }).catch(() => { /* ignore */ });
    }
  } catch {
    // ignore
  }
}

export function install(): void {
  const w = window as any;
  if (w.__WEBSTIR_ERROR_HANDLER_INSTALLED__) return;
  w.__WEBSTIR_ERROR_HANDLER_INSTALLED__ = true;

  window.addEventListener('error', (e) => {
    try { report(e); } catch { /* ignore */ }
  });

  window.addEventListener('unhandledrejection', (e) => {
    try { report(e); } catch { /* ignore */ }
  });
}
