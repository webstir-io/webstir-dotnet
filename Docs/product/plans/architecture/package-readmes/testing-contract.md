# @webstir-io/testing-contract

TypeScript types and JSON schema defining Webstir’s test manifests, runner events, and summaries. Downstream tooling (CLI, dashboards, custom reporters) consume this package to stay aligned with the official contract.

---

## Install

```ini
# .npmrc
@webstir-io:registry=https://npm.pkg.github.com
//npm.pkg.github.com/:_authToken=${GH_PACKAGES_TOKEN}
```

```bash
npm install @webstir-io/testing-contract
```

Only `read:packages` scope is required for consumers.

---

## Exports

```ts
import type {
  TestRuntime,
  TestModule,
  TestManifest,
  TestRunResult,
  RunnerSummary,
  RunnerStartEvent,
  RunnerResultEvent,
  RunnerSummaryEvent,
  RunnerLogEvent,
  RunnerErrorEvent,
  RunnerWatchIterationEvent,
  RunnerEvent,
} from '@webstir-io/testing-contract';
```

- `TestManifest` represents discovered tests (workspace root, timestamp, modules).
- `RunnerEvent` unions all structured events emitted by `@webstir-io/webstir-test`.
- `RunnerSummary` aggregates per-runtime totals and individual `TestRunResult`s.

JSON schema assets ship under `schema/`:

- `TestManifest.schema.json`
- `RunnerEvent.schema.json`

Each schema is hosted at `https://webstir.dev/schema/testing-contract/*.json` and mirrors the TypeScript definitions. They can be used for runtime validation, documentation portals, or contract testing in other languages.

---

## Usage Examples

### Parsing Runner Events

```ts
import { RunnerEvent } from '@webstir-io/testing-contract';

function handleEvent(payload: string): void {
  const event = JSON.parse(payload) as RunnerEvent;
  switch (event.type) {
    case 'summary':
      console.log(`${event.runtime} passed ${event.summary.passed}`);
      break;
    case 'error':
      console.error(event.message);
      break;
  }
}
```

### Validating Against Schema

```ts
import Ajv from 'ajv';
import schema from '@webstir-io/testing-contract/schema/RunnerEvent.schema.json';

const ajv = new Ajv();
const validate = ajv.compile(schema);

const event = JSON.parse(stdin);
if (!validate(event)) {
  console.error('Invalid runner event', validate.errors);
}
```

---

## Maintainer Notes

```bash
npm install
npm run build          # emits dist/index.js, dist/index.d.ts, and refreshed schema/
```

- Regenerate both schema files whenever TypeScript interfaces change.
- Keep the README in sync with publisher workflows in the dedicated repository.
- Ensure GitHub Actions lint, build, and publish artifacts before releasing.

---

## License

MIT © Webstir
