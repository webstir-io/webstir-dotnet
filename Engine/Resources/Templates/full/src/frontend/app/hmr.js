if (typeof window === 'undefined' || typeof document === 'undefined') {
    console.warn('[webstir-hmr] Browser runtime not detected; hot updates disabled.');
} else if (typeof EventSource === 'undefined') {
    console.warn('[webstir-hmr] EventSource API unavailable; falling back to full reloads.');
} else {
    const eventSource = getOrCreateEventSource();
    const updateQueue = [];
    let applyingUpdate = false;
    let reloadScheduled = false;

    eventSource.addEventListener('hmr', (event) => {
        if (!event || !event.data) {
            return;
        }

        try {
            const payload = JSON.parse(event.data);
            enqueueHotUpdate(payload);
        } catch (error) {
            console.error('[webstir-hmr] Failed to parse hot update payload.', error);
            requestReload('payload.parse');
        }
    });

    function enqueueHotUpdate(payload) {
        updateQueue.push(payload);
        void processQueue();
    }

    async function processQueue() {
        if (applyingUpdate || reloadScheduled || updateQueue.length === 0) {
            return;
        }

        const payload = updateQueue.shift();
        if (!payload) {
            return;
        }

        applyingUpdate = true;
        const result = await applyHotUpdate(payload).catch((error) => ({
            success: false,
            reason: 'runtime.error',
            error
        }));
        applyingUpdate = false;

        if (!result.success) {
            requestReload(result.reason, result.error, payload, result.details);
            return;
        }

        if (updateQueue.length > 0) {
            await processQueue();
        }
    }

    async function applyHotUpdate(payload) {
        if (!payload || typeof payload !== 'object') {
            return { success: false, reason: 'payload.invalid' };
        }

        if (payload.requiresReload) {
            return { success: false, reason: 'payload.requiresReload' };
        }

        const modules = Array.isArray(payload.modules) ? payload.modules : [];
        const styles = Array.isArray(payload.styles) ? payload.styles : [];
        const cacheBuster = Date.now().toString(36);
        const baseContext = {
            changedFile: payload.changedFile ?? null,
            modules,
            styles,
            cacheBuster,
            timestamp: Date.now()
        };

        if (modules.length === 0 && styles.length === 0) {
            console.info('[webstir-hmr] Received hot update with no changes.');
            return { success: true };
        }

        const moduleResult = await applyModuleChanges(modules, baseContext);
        if (!moduleResult.success) {
            return moduleResult;
        }

        const styleResult = await applyStyleChanges(styles, baseContext);
        if (!styleResult.success) {
            return styleResult;
        }

        const changedFile = baseContext.changedFile ?? 'unknown';
        console.info(`[webstir-hmr] Applied hot update for ${changedFile}.`);

        return { success: true };
    }

    async function applyModuleChanges(modules, baseContext) {
        if (modules.length === 0) {
            return { success: true };
        }

        for (const asset of modules) {
            if (!isValidAsset(asset)) {
                return { success: false, reason: 'module.invalid', details: asset };
            }

            const context = createModuleContext(baseContext, asset);

            if (!(await invokeDispose(asset, context))) {
                return { success: false, reason: 'module.dispose', details: asset };
            }

            const specifier = withCacheBuster(asset.url, baseContext.cacheBuster);
            let moduleExports;
            try {
                moduleExports = await import(specifier);
            } catch (error) {
                console.error(`[webstir-hmr] Failed to import module '${asset.url}'.`, error);
                return { success: false, reason: 'module.import', error, details: asset };
            }

            if (!(await invokeAccept(moduleExports, context))) {
                console.warn(`[webstir-hmr] Accept handler declined update for '${asset.relativePath}'.`);
                return { success: false, reason: 'module.declined', details: asset };
            }
        }

        return { success: true };
    }

    async function applyStyleChanges(styles, baseContext) {
        if (styles.length === 0) {
            return { success: true };
        }

        for (const asset of styles) {
            if (!isValidAsset(asset)) {
                return { success: false, reason: 'style.invalid', details: asset };
            }

            const success = await swapStylesheet(asset, baseContext.cacheBuster);
            if (!success) {
                return { success: false, reason: 'style.swap', details: asset };
            }
        }

        return { success: true };
    }

    function createModuleContext(baseContext, asset) {
        return {
            changedFile: baseContext.changedFile,
            modules: baseContext.modules,
            styles: baseContext.styles,
            cacheBuster: baseContext.cacheBuster,
            timestamp: baseContext.timestamp,
            asset
        };
    }

    async function invokeDispose(asset, context) {
        const handler = window.__webstirDispose;
        if (typeof handler !== 'function') {
            return true;
        }

        try {
            const result = handler(asset, context);
            if (isPromise(result)) {
                await result;
            }
            return true;
        } catch (error) {
            console.error(`[webstir-hmr] Dispose handler threw for '${asset.relativePath}'.`, error);
            return false;
        }
    }

    async function invokeAccept(moduleExports, context) {
        const handler = window.__webstirAccept;
        if (typeof handler !== 'function') {
            return true;
        }

        try {
            const result = handler(moduleExports, context);
            if (isPromise(result)) {
                const resolved = await result;
                return resolved !== false;
            }
            return result !== false;
        } catch (error) {
            console.error('[webstir-hmr] Accept handler threw.', error);
            return false;
        }
    }

    function swapStylesheet(asset, cacheBuster) {
        return new Promise((resolve) => {
            const specifier = withCacheBuster(asset.url, cacheBuster);
            const existingLinks = Array.from(document.querySelectorAll('link[rel="stylesheet"]'));
            const target = existingLinks.find((link) => normalizePath(link.href) === normalizePath(asset.url));
            const replacement = document.createElement('link');
            replacement.rel = 'stylesheet';
            replacement.href = specifier;

            replacement.addEventListener('load', () => {
                if (target && target.parentNode) {
                    requestAnimationFrame(() => target.remove());
                }
                resolve(true);
            });

            replacement.addEventListener('error', () => {
                replacement.remove();
                resolve(false);
            });

            if (target && target.parentNode) {
                target.after(replacement);
            } else {
                document.head.appendChild(replacement);
            }
        });
    }

    function requestReload(reason, error, payload, details) {
        if (reloadScheduled) {
            return;
        }

        reloadScheduled = true;

        if (error) {
            console.error('[webstir-hmr] Hot update failed.', error);
        }

        const changedFile = payload?.changedFile ?? 'unknown';
        const fallbackReasons = Array.isArray(payload?.fallbackReasons) && payload.fallbackReasons.length > 0
            ? ` Fallback reasons: ${payload.fallbackReasons.join(', ')}.`
            : '';
        console.warn(
            `[webstir-hmr] Falling back to full reload for ${changedFile}. ` +
            `Reason: ${reason ?? 'unknown'}.${fallbackReasons}`
        );

        setStatus('hmr-fallback', 'Hot update fallback – reloading…');
        notifyFallback(reason, payload, details);
        updateQueue.length = 0;
        setTimeout(() => window.location.reload(), 0);
    }

    function setStatus(status, message) {
        const setter = window.__webstirSetDevStatus;
        if (typeof setter === 'function') {
            try {
                setter(status, message);
            } catch (error) {
                console.debug('[webstir-hmr] Status handler failed.', error);
            }
        }
    }

    function notifyFallback(reason, payload, details) {
        const handler = window.__webstirOnHmrFallback;
        if (typeof handler === 'function') {
            try {
                handler({ reason, payload, details });
            } catch (error) {
                console.debug('[webstir-hmr] Fallback hook threw.', error);
            }
        }
    }

    function readStats(candidate) {
        if (!candidate || typeof candidate !== 'object') {
            return null;
        }

        const hotUpdates = coerceInteger(candidate.hotUpdates);
        const reloadFallbacks = coerceInteger(candidate.reloadFallbacks);

        if (hotUpdates === null || reloadFallbacks === null) {
            return null;
        }

        return {
            hotUpdates,
            reloadFallbacks
        };
    }

    function coerceInteger(value) {
        if (typeof value === 'number' && Number.isFinite(value)) {
            return Math.trunc(value);
        }

        if (typeof value === 'string') {
            const parsed = Number.parseInt(value, 10);
            if (Number.isFinite(parsed)) {
                return parsed;
            }
        }

        return null;
    }

    function getOrCreateEventSource() {
        if (window.__webstirEventSource instanceof EventSource) {
            return window.__webstirEventSource;
        }

        const source = new EventSource('/sse');
        window.__webstirEventSource = source;
        return source;
    }

    function withCacheBuster(url, cacheBuster) {
        if (typeof url !== 'string' || url.length === 0) {
            return url;
        }

        try {
            const parsed = new URL(url, window.location.origin);
            parsed.searchParams.set('hmr', cacheBuster);
            return `${parsed.pathname}${parsed.search}${parsed.hash}`;
        } catch {
            const separator = url.includes('?') ? '&' : '?';
            return `${url}${separator}hmr=${cacheBuster}`;
        }
    }

    function normalizePath(url) {
        if (typeof url !== 'string') {
            return '';
        }

        try {
            return new URL(url, window.location.origin).pathname;
        } catch {
            const index = url.indexOf('?');
            return index === -1 ? url : url.slice(0, index);
        }
    }

    function isValidAsset(asset) {
        return Boolean(asset && typeof asset.url === 'string' && asset.url.length > 0);
    }

    function isPromise(value) {
        return !!value && typeof value.then === 'function';
    }
 }
