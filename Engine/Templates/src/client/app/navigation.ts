import { router } from './router.js';

export function navigate(url: string) {
  return router.navigate(url);
}

export { router } from './router.js';
export type { RouteHandler, RouteParams } from '@shared/router-types.js';