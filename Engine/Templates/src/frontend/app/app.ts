// Folder-based routing: Only initialize if router exists
// Pages can export a routeHandler for SPA behavior

async function initializeRouting() {
  try {
    await import('./router.js');
    // Router exists, so SPA routing is available
    // Individual pages will register their own routes if they have routeHandlers
  } catch {
    // No router.js file, app doesn't use SPA routing
  }
}

initializeRouting();
