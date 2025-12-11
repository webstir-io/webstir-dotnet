// Basic Home page test: verifies merged HTML has expected parts
// The default provider is configured via WEBSTIR_TESTING_PROVIDER or webstir.providers.json.
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { test, assert } from '@webstir-io/webstir-testing';

// Node runs this test as ESM; derive the directory from the module URL.
const currentDir = dirname(fileURLToPath(import.meta.url));

// Built HTML is at build/frontend/pages/home/index.html relative to the compiled test output.
test('home page has expected parts', () => {
  const htmlPath = resolve(currentDir, '..', 'index.html');
  const html = readFileSync(htmlPath, 'utf8');

  assert.isTrue(html.includes('<title>Webstir SSG Demo</title>'), 'Missing demo page title');
  assert.isTrue(html.includes('<link rel="stylesheet" href="index.css"'), 'Missing CSS link to index.css');
  assert.isTrue(html.includes('<main'), 'Missing <main> container');
  assert.isTrue(html.includes('Webstir SSG demo'), 'Missing hero heading content');
});
