// Basic Home page test: verifies merged HTML has expected parts
// Use CommonJS require to avoid ESM in the runner context
const fs = require('node:fs');
const path = require('node:path');

// __dirname is provided by the runner context and points to build/.../tests
// Built HTML is at build/client/pages/home/index.html

test('home page has expected parts', () => {
  const htmlPath = path.resolve(__dirname, '..', 'index.html');
  const html = fs.readFileSync(htmlPath, 'utf8');

  assert.isTrue(html.includes('<title>Home</title>'), 'Missing <title>Home</title>');
  assert.isTrue(html.includes('<link rel="stylesheet" href="index.css"'), 'Missing CSS link to index.css');
  assert.isTrue(html.includes('<script type="module" src="index.js"'), 'Missing module script to index.js');
  assert.isTrue(html.includes('<main'), 'Missing <main> container');
  assert.isTrue(html.includes('Home'), 'Missing Home content');
});
