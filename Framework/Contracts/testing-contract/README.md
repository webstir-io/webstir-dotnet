# @webstir-io/testing-contract

TypeScript types and JSON schema defining Webstir’s test manifests, runner events, and summaries. Downstream tooling (CLI, dashboards, custom reporters) consume this package to stay aligned with the official testing contract.

## Install

```ini
# .npmrc
@webstir-io:registry=https://npm.pkg.github.com
//npm.pkg.github.com/:_authToken=${GH_PACKAGES_TOKEN}
```

```bash
npm install @webstir-io/testing-contract
```

Consumers require `read:packages` scope; publishers need `write:packages`.

## Exported Types

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

- `TestManifest` documents discovered tests (workspace root, timestamp, modules).
- `RunnerEvent` unions all structured events emitted by `@webstir-io/webstir-testing`.
- `RunnerSummary` aggregates totals and individual `TestRunResult`s.

Schema artifacts are published under `schema/`:

- `TestManifest.schema.json`
- `RunnerEvent.schema.json`

The schemas are also hosted at `https://webstir.dev/schema/testing-contract/*.json` for tooling that prefers remote references.

## Usage Examples

### Handling Runner Events

```ts
import type { RunnerEvent } from '@webstir-io/testing-contract';

function onEvent(payload: string) {
  const event = JSON.parse(payload) as RunnerEvent;
  if (event.type === 'summary') {
    console.log(`${event.runtime} passed ${event.summary.passed}`);
  }
}
```

### Validating With JSON Schema

```ts
import Ajv from 'ajv';
import schema from '@webstir-io/testing-contract/schema/RunnerEvent.schema.json';

const ajv = new Ajv();
const validate = ajv.compile(schema);

function assertEvent(payload: string) {
  const event = JSON.parse(payload);
  if (!validate(event)) {
    throw new Error(`Invalid runner event: ${ajv.errorsText(validate.errors)}`);
  }
}
```

## Maintainer Workflow

```bash
npm install
npm run build          # emits dist/index.js, dist/index.d.ts, refreshed schema/
```

- Regenerate schema files whenever TypeScript interfaces change.
- Ensure CI lints, builds, and verifies schema parity before publishing.

## License

MIT © Webstir
