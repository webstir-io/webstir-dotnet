#!/usr/bin/env node
import fs from 'node:fs/promises';
import path from 'node:path';

const PACKAGE_PATHS = [
  'Framework/Frontend/package.json',
  'Framework/Testing/package.json'
];
const LOCK_PATHS = [
  'Framework/Frontend/package-lock.json',
  'Framework/Testing/package-lock.json'
];

function parseArguments() {
  const args = process.argv.slice(2);
  let expected;

  for (let i = 0; i < args.length; i += 1) {
    const arg = args[i];
    if (arg === '--expected' || arg === '-e') {
      expected = args[i + 1];
      i += 1;
    } else {
      throw new Error(`Unknown argument: ${arg}`);
    }
  }

  if (!expected) {
    throw new Error('Missing --expected <version> argument');
  }

  return expected;
}

async function readJson(relativePath) {
  const absolute = path.resolve(relativePath);
  const content = await fs.readFile(absolute, 'utf8');
  return JSON.parse(content);
}

async function main() {
  const expected = parseArguments();

  for (const packagePath of PACKAGE_PATHS) {
    const data = await readJson(packagePath);
    if (data.version !== expected) {
      throw new Error(`${packagePath} has version ${data.version}, expected ${expected}`);
    }
  }

  for (const lockPath of LOCK_PATHS) {
    const data = await readJson(lockPath);
    if (data.version !== expected) {
      throw new Error(`${lockPath} has version ${data.version}, expected ${expected}`);
    }
    if (data.packages && data.packages[''] && data.packages[''].version !== expected) {
      throw new Error(`${lockPath} root package entry has version ${data.packages[''].version}, expected ${expected}`);
    }
  }
}

main().catch(error => {
  console.error(error.message);
  process.exit(1);
});
