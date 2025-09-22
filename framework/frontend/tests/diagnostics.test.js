import test from 'node:test';
import assert from 'node:assert/strict';

import { emitDiagnostic, STRUCTURED_DIAGNOSTIC_PREFIX } from '../dist/core/index.js';

test('emitDiagnostic emits human-readable and structured output', () => {
    const originalLog = console.log;
    const originalWarn = console.warn;
    const logs = [];
    const warnings = [];

    console.log = (message) => {
        logs.push(message);
    };
    console.warn = (message) => {
        warnings.push(message);
    };

    try {
        emitDiagnostic({
            code: 'frontend.test.warning',
            kind: 'test',
            stage: 'unit',
            severity: 'warning',
            message: 'Sample diagnostic for testing.',
            data: { flag: true }
        });
    } finally {
        console.log = originalLog;
        console.warn = originalWarn;
    }

    assert.equal(warnings.length, 1);
    assert.match(warnings[0], /\[webstir-frontend]\[frontend\.test\.warning\] Sample diagnostic for testing\./);

    assert.equal(logs.length, 1);
    const structuredLine = logs[0];
    assert.ok(structuredLine.startsWith(STRUCTURED_DIAGNOSTIC_PREFIX));

    const payload = JSON.parse(structuredLine.slice(STRUCTURED_DIAGNOSTIC_PREFIX.length));
    assert.equal(payload.type, 'diagnostic');
    assert.equal(payload.code, 'frontend.test.warning');
    assert.equal(payload.kind, 'test');
    assert.equal(payload.stage, 'unit');
    assert.equal(payload.severity, 'warning');
    assert.equal(payload.message, 'Sample diagnostic for testing.');
    assert.deepEqual(payload.data, { flag: true });
});
