import type { RoutingMetadata, PageRouteInfo } from '@shared/router-types.js';
import type { Router } from './router.js';

initializeRoutingIfNeeded();

function initializeRoutingIfNeeded() {
  const routingMetadata = loadRoutingMetadata();
  if (!routingMetadata || !routingMetadata.hasSpaPages) {
    return;
  }

  enableSpaRouting(routingMetadata);
}

function loadRoutingMetadata(): RoutingMetadata | null {
  const metadataElement = document.getElementById('app-routing-metadata');
  if (!metadataElement?.textContent) {
    return null;
  }
  
  try {
    return JSON.parse(metadataElement.textContent);
  } catch {
    return null;
  }
}

async function enableSpaRouting(routingMetadata: RoutingMetadata) {
  const { router } = await import('./router.js');
  
  for (const [pageName, pageInfo] of Object.entries(routingMetadata.pages)) {
    if (pageInfo.isSpaEnabled) {
      registerPageRoute(router, pageName, pageInfo);
    }
  }
}

async function registerPageRoute(router: Router, pageName: string, _pageInfo: PageRouteInfo) {
  const routePath = pageName === 'index' ? '/' : `/${pageName}/`;
  const moduleUrl = `../pages/${pageName}/${pageName}.js`;
  
  try {
    const pageModule = await import(moduleUrl);
    if (pageModule.routeHandler) {
      router.registerRoute(routePath, pageModule.routeHandler);
    }
  } catch (error) {
    console.warn(`Failed to load route handler for ${pageName}:`, error);
  }
}