# Getting Started

Install prerequisites and run the CLI locally.

## Prerequisites
- .NET SDK 9.0+
- Node.js 20+ and npm
- TypeScript compiler on PATH (`npm i -g typescript` or project-local)

## Steps
1) Build the CLI or run via dotnet

```
# From repo root (run via dotnet)
dotnet run --project CLI -- --help
```

2) Create a new project and start dev mode

```
webstir init my-app
cd my-app
webstir watch
```

3) Open the printed dev server URL. Edit files under `src/**` to see live reload.

## Next
- Add a page — ../how-to/add-page.md
- Run tests — ../how-to/test.md
- Publish — ../how-to/publish.md
