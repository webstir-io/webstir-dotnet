// Minimal TS test runner file
(async () => {
  function now() { return Date.now(); }
  const registry = new Map(); // file -> [{ name, fn }]

  function ensureList(file) {
    if (!registry.has(file)) registry.set(file, []);
    return registry.get(file);
  }

  function defineTest(name, fn) {
    if (typeof __currentFile !== 'string') throw new Error('No current file set');
    ensureList(__currentFile).push({ name: String(name), fn: fn || (async () => {}) });
  }

  function fail(msg) { const err = new Error(String(msg || 'Assertion failed')); err.__assert = true; throw err; }
  function isTrue(value, message) { if (!value) fail(message || `Expected truthy but got ${value}`); }
  function equal(expected, actual, message) {
    const ok = Object.is(expected, actual);
    if (!ok) fail(message || `Expected ${JSON.stringify(expected)} but got ${JSON.stringify(actual)}`);
  }

  globalThis.test = defineTest;
  globalThis.assert = { isTrue, equal, fail };

  const fs = require('fs');
  const vm = require('vm');
  const path = require('path');
  const Module = require('module');

  const assertApi = Object.freeze({ isTrue, equal, fail });
  const testModuleExports = Object.freeze({
    test: defineTest,
    assert: assertApi
  });

  function createRuntimeRequire(file) {
    const baseRequire = Module.createRequire(file);
    function runtimeRequire(specifier) {
      if (specifier === '@webstir/test') {
        return testModuleExports;
      }
      return baseRequire(specifier);
    }

    // Mirror selected properties expected on require
    runtimeRequire.resolve = (specifier, options) => {
      if (specifier === '@webstir/test') {
        return specifier;
      }
      return baseRequire.resolve(specifier, options);
    };
    runtimeRequire.cache = baseRequire.cache;
    runtimeRequire.main = baseRequire.main;
    runtimeRequire.extensions = baseRequire.extensions;

    return runtimeRequire;
  }

  function evaluateModule(file) {
    const code = fs.readFileSync(file, 'utf8');
    const runtimeRequire = createRuntimeRequire(file);
    const context = vm.createContext({
      // expose the runner's globals so user code sees them as bare identifiers
      test: defineTest,
      assert: assertApi,
      // also expose the real globalThis for advanced cases
      globalThis,
      console,
      setTimeout,
      clearTimeout,
      require: runtimeRequire,
      __dirname: path.dirname(file),
      __filename: file,
    });
    globalThis.__currentFile = file;
    try {
      const script = new vm.Script(code, { filename: file, displayErrors: true });
      script.runInContext(context, { displayErrors: true });
      return null; // no error
    } catch (e) {
      return e && (e.stack || String(e));
    } finally {
      delete globalThis.__currentFile;
    }
  }

  async function runOne(test) {
    const start = now();
    try {
      const r = test.fn();
      if (r && typeof r.then === 'function') { await r; }
      return { passed: true, durationMs: now() - start };
    } catch (e) {
      const msg = e && e.stack ? String(e.stack) : String(e);
      return { passed: false, message: msg, durationMs: now() - start };
    }
  }

  async function run(files) {
    const allResults = [];
    const runStart = now();
    for (const file of files) {
      if (!fs.existsSync(file)) {
        allResults.push({ name: '[missing compiled file]', file, passed: false, message: 'Compiled file not found', durationMs: 0 });
        continue;
      }

      const evalError = evaluateModule(file);
      if (evalError) {
        allResults.push({ name: '[module evaluation]', file, passed: false, message: String(evalError), durationMs: 0 });
        continue;
      }

      const tests = registry.get(file) || [];
      for (const t of tests) {
        const r = await runOne(t);
        allResults.push({ name: t.name, file, passed: r.passed, message: r.message || null, durationMs: r.durationMs });
      }
    }

    let passed = 0, failed = 0;
    for (const r of allResults) { if (r.passed) passed++; else failed++; }
    const total = allResults.length;
    const durationMs = now() - runStart;
    const result = { passed, failed, total, durationMs, results: allResults };
    process.stdout.write(JSON.stringify(result));
  }

  // Read JSON array of files from stdin
  const chunks = [];
  for await (const c of process.stdin) { chunks.push(c); }
  const input = Buffer.concat(chunks).toString('utf8') || '[]';
  let files;
  try { files = JSON.parse(input); } catch { files = []; }
  await run(Array.isArray(files) ? files : []);
})();
