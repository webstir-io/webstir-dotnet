#!/usr/bin/env node
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { Validator } from 'jsonschema';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repositoryRoot = path.resolve(__dirname, '..', '..');

function loadJson(relativePath) {
  const absolutePath = path.join(repositoryRoot, relativePath);
  const contents = fs.readFileSync(absolutePath, 'utf8');
  return JSON.parse(contents);
}

const validator = new Validator();

const manifestSchemaPath = 'Framework/Contracts/testing-contract/schema/TestManifest.schema.json';
const manifestSchema = loadJson(manifestSchemaPath);
validator.addSchema(manifestSchema, manifestSchema.$id);

const validManifest = {
  workspaceRoot: '/tmp/workspace',
  generatedAt: new Date().toISOString(),
  modules: [
    {
      id: 'frontend/pages/home/tests/home',
      runtime: 'frontend',
      sourcePath: '/tmp/workspace/src/frontend/pages/home/tests/home.test.ts',
      compiledPath: '/tmp/workspace/build/frontend/pages/home/tests/home.test.js'
    }
  ]
};

const manifestResult = validator.validate(validManifest, manifestSchema);
assert.equal(
  manifestResult.errors.length,
  0,
  `Expected manifest schema validation to pass but found: ${manifestResult.errors.map((error) => error.stack).join(', ')}`
);

const invalidManifest = {
  workspaceRoot: '/tmp/workspace',
  generatedAt: new Date().toISOString(),
  modules: [
    {
      id: 'frontend/pages/home/tests/home',
      runtime: 'frontend',
      sourcePath: '/tmp/workspace/src/frontend/pages/home/tests/home.test.ts'
    }
  ]
};

const invalidManifestResult = validator.validate(invalidManifest, manifestSchema);
assert.notEqual(
  invalidManifestResult.errors.length,
  0,
  'Expected invalid manifest to fail schema validation'
);

const eventSchemaPath = 'Framework/Contracts/testing-contract/schema/RunnerEvent.schema.json';
const eventSchema = loadJson(eventSchemaPath);
validator.addSchema(eventSchema, eventSchema.$id);

const validEvent = {
  type: 'summary',
  runId: 'sample-run',
  runtime: 'all',
  summary: {
    passed: 1,
    failed: 0,
    total: 1,
    durationMs: 42,
    results: [
      {
        name: 'passes',
        file: '/tmp/workspace/build/frontend/pages/home/tests/home.test.js',
        passed: true,
        message: null,
        durationMs: 42
      }
    ]
  }
};

const eventResult = validator.validate(validEvent, eventSchema);
assert.equal(
  eventResult.errors.length,
  0,
  `Expected event schema validation to pass but found: ${eventResult.errors.map((error) => error.stack).join(', ')}`
);

const invalidEvent = {
  type: 'summary',
  runId: 'missing-total',
  runtime: 'all',
  summary: {
    passed: 1,
    failed: 0,
    durationMs: 42,
    results: []
  }
};

const invalidEventResult = validator.validate(invalidEvent, eventSchema);
assert.notEqual(
  invalidEventResult.errors.length,
  0,
  'Expected invalid event to fail schema validation'
);

console.log('Contract schemas validated successfully.');
