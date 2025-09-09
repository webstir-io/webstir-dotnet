# Your First App

Build a simple multi-page app and see live reload and API proxying.

## Create the app
```
webstir init my-first-app
cd my-first-app
```

## Run in dev mode
```
webstir watch
```

## Add a page
```
webstir add-page about
```

Open `/about` in the browser; edit files in `src/frontend/pages/about/` and observe live reload.

## Call the API
Use the template API under `/api/health`. Requests are proxied by the dev server to the Node API.

## Test and publish
```
webstir test
webstir publish
```

View `dist/frontend/pages/<page>/` for production outputs and manifests.

## Next
- Explore the pipelines — ../explanations/pipelines.md
- Sandbox with Docker — ../how-to/sandbox.md
