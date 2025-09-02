import type { RouteHandler, RouteParams, RoutingMetadata } from '@shared/router-types.js';

export class Router {
  private routes = new Map<string, RouteHandler>();
  private currentHandler: RouteHandler | null = null;
  
  constructor() {
    this.loadRoutingMetadataFromDom();
    this.setupBrowserNavigation();
    this.interceptLinkClicks();
  }
  
  registerRoute(path: string, handler: RouteHandler) {
    this.routes.set(path, handler);
  }
  
  async navigate(url: string) {
    const targetPath = new URL(url, window.location.origin).pathname;
    const routeHandler = this.routes.get(targetPath);
    
    if (!routeHandler) {
      window.location.href = url;
      return;
    }
    
    window.history.pushState({}, '', url);
    await this.handleRouteChange();
  }
  
  private loadRoutingMetadataFromDom() {
    const metadataElement = document.getElementById('app-routing-metadata');
    if (!metadataElement?.textContent) return;
    
    try {
      const metadata: RoutingMetadata = JSON.parse(metadataElement.textContent);
    } catch (error) {
      console.warn('Failed to parse routing metadata:', error);
    }
  }
  
  private setupBrowserNavigation() {
    window.addEventListener('popstate', () => {
      this.handleRouteChange();
    });
  }
  
  private interceptLinkClicks() {
    document.addEventListener('click', (event) => {
      const clickedLink = (event.target as HTMLElement).closest('a');
      if (!clickedLink || !this.shouldInterceptLink(clickedLink)) return;
      
      event.preventDefault();
      this.navigate(clickedLink.href);
    });
  }
  
  private async handleRouteChange() {
    const currentPath = window.location.pathname;
    const routeParams = this.extractQueryParams();
    
    await this.callOnLeaveHandler();
    
    const newHandler = this.routes.get(currentPath);
    if (newHandler) {
      this.currentHandler = newHandler;
      await this.callOnEnterHandler(newHandler, routeParams);
    }
  }
  
  private async callOnLeaveHandler() {
    if (this.currentHandler?.onLeave) {
      await this.currentHandler.onLeave();
    }
  }
  
  private async callOnEnterHandler(handler: RouteHandler, params: RouteParams) {
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

export const router = new Router();