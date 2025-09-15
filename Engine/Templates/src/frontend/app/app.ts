// Global app initialization

// Lazy-load error handler on first error
let errorHandlerLoaded = false;

async function loadErrorHandler() {
  if (errorHandlerLoaded) return;
  errorHandlerLoaded = true;

  try {
    const { install } = await import('./error');
    install();
  } catch (e) {
    console.error('Failed to load error handler:', e);
  }
}

// Set up error listeners that will dynamically import the error handler
window.addEventListener('error', async (e) => {
  await loadErrorHandler();
  // The installed handler will catch subsequent errors
});

window.addEventListener('unhandledrejection', async (e) => {
  await loadErrorHandler();
  // The installed handler will catch subsequent rejections
});

// Export for use by pages if needed
export { loadErrorHandler };