import type { RouteHandler, RouteParams } from './router-types.js';

export class Router {
    private routes = new Map<string, RouteHandler>();
    private currentHandler: RouteHandler | null = null;

    public constructor() {
        this.setupBrowserNavigation();
        this.interceptLinkClicks();
    }

    public registerRoute(path: string, handler: RouteHandler): void {
        this.routes.set(path, handler);
    }

    public async navigate(url: string): Promise<void> {
        const targetPath = new URL(url, window.location.origin).pathname;
        const routeHandler = this.routes.get(targetPath);

        if (!routeHandler) {
            window.location.href = url;
            return;
        }

        window.history.pushState({}, '', url);
        await this.handleRouteChange();
    }

    private setupBrowserNavigation(): void {
        window.addEventListener('popstate', () => {
            this.handleRouteChange();
        });
    }

    private interceptLinkClicks(): void {
        document.addEventListener('click', (event) => {
            const target = event.target as EventTarget | null;
            if (!(target instanceof Element)) {
                return;
            }
            if (event.defaultPrevented || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
                return;
            }

            const clickedLink = target.closest('a');
            if (!clickedLink || !this.shouldInterceptLink(clickedLink as HTMLAnchorElement)) {
                return;
            }

            event.preventDefault();
            this.navigate((clickedLink as HTMLAnchorElement).href);
        });
    }

    private async handleRouteChange(): Promise<void> {
        const currentPath = window.location.pathname;
        const routeParams = this.extractQueryParams();

        await this.callOnLeaveHandler();

        const newHandler = this.routes.get(currentPath);
        if (newHandler) {
            this.currentHandler = newHandler;
            await this.callOnEnterHandler(newHandler, routeParams);
        }
    }

    private async callOnLeaveHandler(): Promise<void> {
        if (this.currentHandler?.onLeave) {
            await this.currentHandler.onLeave();
        }
    }

    private async callOnEnterHandler(handler: RouteHandler, params: RouteParams): Promise<void> {
        if (handler.onEnter) {
            await handler.onEnter(params);
        }
    }

    private shouldInterceptLink(link: HTMLAnchorElement): boolean {
        const isExternalLink = link.origin !== window.location.origin;
        const isDownloadLink = link.hasAttribute('download');
        const opensInNewTab = link.getAttribute('target') === '_blank';
        const hasRouteHandler = this.routes.has(link.pathname);

        return !isExternalLink && !isDownloadLink && !opensInNewTab && hasRouteHandler;
    }

    private extractQueryParams(): RouteParams {
        const params: RouteParams = {};
        const urlSearchParams = new URLSearchParams(window.location.search);

        urlSearchParams.forEach((value, key) => {
            params[key] = value;
        });

        return params;
    }
}

let singleton: Router | null = null;

export function startRouter(): Router {
    if (!singleton) {
        singleton = new Router();
    }
    return singleton;
}

export function getRouter(): Router | null {
    return singleton;
}

export function navigate(url: string): Promise<void> {
    return startRouter().navigate(url);
}

export type { RouteHandler, RouteParams };
