#!/usr/bin/env node

import { mkdir, writeFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

import { zodToJsonSchema } from 'zod-to-json-schema';

import {
  moduleErrorSchema,
  moduleManifestSchema,
  routeDefinitionSchema,
  routeInputSchema,
  routeOutputSchema,
  viewDefinitionSchema
} from '../dist/index.js';

const __dirname = dirname(fileURLToPath(import.meta.url));
const schemaDir = join(__dirname, '..', 'schema');

await mkdir(schemaDir, { recursive: true });

const schemaEntries = [
  ['module-manifest', moduleManifestSchema],
  ['route-definition', routeDefinitionSchema],
  ['route-input', routeInputSchema],
  ['route-output', routeOutputSchema],
  ['view-definition', viewDefinitionSchema],
  ['module-error', moduleErrorSchema]
];

const toJson = (name, schema) =>
  zodToJsonSchema(schema, name, {
    target: 'jsonSchema7',
    $refStrategy: 'none'
  });

await Promise.all(
  schemaEntries.map(async ([name, schema]) => {
    const json = toJson(name, schema);
    const filePath = join(schemaDir, `${name}.schema.json`);
    const contents = `${JSON.stringify(json, null, 2)}\n`;
    await writeFile(filePath, contents, 'utf8');
    console.info(`[module-contract] wrote ${filePath}`);
  })
);
