// Global client-side error reporter
(function () {
  var lastSentAt = 0;
  var sentCount = 0;
  var MAX_PER_SESSION = 20;
  var MIN_INTERVAL_MS = 1000;
  var DEDUPE_WINDOW_MS = 60000; // 60s
  var recent = new Map(); // fingerprint -> timestamp

  function cid() {
    if (!window.__WEBSTIR_CID__) {
      window.__WEBSTIR_CID__ = 'c-' + Math.random().toString(36).slice(2) + Date.now().toString(36);
    }
    return window.__WEBSTIR_CID__;
  }

  function toPayload(e) {
    var isRejection = e && e.type === 'unhandledrejection';
    var reason = isRejection ? (e.reason || {}) : {};
    var err = (e && e.error) || reason || {};
    var message = (e && e.message) || reason.message || (err && err.message) || 'Unknown error';
    var stack = (err && err.stack) || reason.stack || '';
    var filename = (e && e.filename) || '';
    var lineno = (e && e.lineno) || 0;
    var colno = (e && e.colno) || 0;
    return {
      type: isRejection ? 'unhandledrejection' : 'error',
      message: String(message || ''),
      stack: String(stack || ''),
      filename: String(filename || ''),
      lineno: Number(lineno || 0),
      colno: Number(colno || 0),
      pageUrl: String(location.href),
      userAgent: String(navigator.userAgent || ''),
      timestamp: new Date().toISOString(),
      correlationId: cid()
    };
  }

  function hash(str) {
    // Simple 32-bit FNV-1a
    var h = 2166136261;
    for (var i = 0; i < str.length; i++) {
      h ^= str.charCodeAt(i);
      h += (h << 1) + (h << 4) + (h << 7) + (h << 8) + (h << 24);
    }
    return (h >>> 0).toString(36);
  }

  function fingerprint(p) {
    var key = [p.type || '', p.message || '', (p.filename || '') + ':' + (p.lineno || 0) + ':' + (p.colno || 0), hash(p.stack || '')].join('|');
    return key;
  }

  function shouldSend(p) {
    var now = Date.now();
    if ((now - lastSentAt) < MIN_INTERVAL_MS) return false;
    if (sentCount >= MAX_PER_SESSION) return false;
    // prune and check dedupe window
    var fp = fingerprint(p);
    var last = recent.get(fp) || 0;
    // prune old entries opportunistically
    if (recent.size > 100) {
      recent.forEach(function (ts, k) { if (now - ts > DEDUPE_WINDOW_MS) recent.delete(k); });
    }
    if (now - last < DEDUPE_WINDOW_MS) return false;
    recent.set(fp, now);
    lastSentAt = now;
    sentCount++;
    return true;
  }

  function report(e) {
    try {
      var p = toPayload(e);
      if (!shouldSend(p)) return;
      var payload = JSON.stringify(p);
      if (navigator.sendBeacon) {
        var blob = new Blob([payload], { type: 'application/json' });
        navigator.sendBeacon('/client-errors', blob);
      } else {
        fetch('/client-errors', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', 'X-Correlation-ID': cid() },
          body: payload,
          keepalive: true
        }).catch(function () { /* ignore */ });
      }
    } catch (_) { /* ignore */ }
  }

  window.__WEBSTIR_ON_ERROR__ = window.__WEBSTIR_ON_ERROR__ || report;
  window.addEventListener('error', function (e) { try { window.__WEBSTIR_ON_ERROR__(e); } catch (_) {} });
  window.addEventListener('unhandledrejection', function (e) { try { window.__WEBSTIR_ON_ERROR__(e); } catch (_) {} });
})();
