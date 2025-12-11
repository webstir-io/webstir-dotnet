// TypeScript file for index page

import { registerHotModule } from '../../app/app';

const main = document.querySelector('main');
if (main) {
  main.dataset.hmrRendered = String(Date.now());
}

registerHotModule(import.meta.url, {
  accept: (_, context) => {
    console.info('[webstir-hmr] Home page accepted update for', context.asset?.relativePath ?? 'unknown module');
    return true;
  },
  dispose: (context) => {
    console.info('[webstir-hmr] Preparing to update', context.asset?.relativePath ?? 'home page module');
  }
});
