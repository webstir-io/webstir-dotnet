import { startRouter } from './router.js';

export function navigate(url: string) {
  return startRouter().navigate(url);
}

export { startRouter } from './router.js';
export type { RouteHandler, RouteParams } from '@shared/router-types.js';
