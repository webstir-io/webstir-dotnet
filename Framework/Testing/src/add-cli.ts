#!/usr/bin/env node
import path from 'node:path';
import fs from 'fs-extra';

async function main(): Promise<void> {
  const args = process.argv.slice(2);
  const workspaceIndex = args.findIndex((arg) => arg === '--workspace' || arg === '-w');
  if (workspaceIndex < 0 || workspaceIndex + 1 >= args.length) {
    throw new Error('Missing required --workspace <path> option.');
  }

  const workspaceRoot = path.resolve(args[workspaceIndex + 1]);
  const nameArg = args.find((arg, index) => index !== workspaceIndex && index !== workspaceIndex + 1 && !arg.startsWith('-'));

  if (!nameArg) {
    throw new Error('Missing test name. Usage: webstir-testing-add <name> --workspace <path>');
  }

  const normalized = normalizeName(nameArg);
  const srcRoot = path.join(workspaceRoot, 'src');
  const hasSlash = normalized.includes('/');

  let targetDirectory: string;
  let fileName: string;

  if (hasSlash) {
    const withoutExtension = normalized;
    const parent = path.posix.dirname(withoutExtension);
    const leaf = path.posix.basename(withoutExtension);
    targetDirectory = path.join(srcRoot, parent, 'tests');
    fileName = `${leaf}.test.ts`;
  } else {
    targetDirectory = path.join(srcRoot, 'tests');
    fileName = `${normalized}.test.ts`;
  }

  await fs.ensureDir(targetDirectory);
  const targetFile = path.join(targetDirectory, fileName);

  if (await fs.pathExists(targetFile)) {
    console.log(`File already exists: ${path.relative(workspaceRoot, targetFile)}`);
    return;
  }

  await fs.writeFile(targetFile, SAMPLE_TEST_TEMPLATE, 'utf8');
  console.log(`Created ${path.relative(workspaceRoot, targetFile)}`);
}

function normalizeName(raw: string): string {
  const trimmed = raw.trim().replace(/\\/g, '/');
  return trimmed.replace(/(\.test\.ts)$/i, '');
}

const SAMPLE_TEST_TEMPLATE = `import { test, assert } from '@webstir-io/webstir-testing';

test('sample passes', () => {
  assert.isTrue(true);
});
`;

void main().catch((error) => {
  const message = error instanceof Error ? error.message : String(error);
  console.error(message);
  process.exitCode = 1;
});
