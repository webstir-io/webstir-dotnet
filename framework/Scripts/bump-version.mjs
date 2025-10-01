#!/usr/bin/env node
import fs from 'node:fs/promises';
import path from 'node:path';

const PACKAGE_PATHS = [
  'framework/frontend/package.json',
  'framework/testing/package.json'
];
const LOCK_PATHS = [
  'framework/frontend/package-lock.json',
  'framework/testing/package-lock.json'
];

function parseArguments() {
  const args = process.argv.slice(2);
  let bump = 'patch';
  let dryRun = false;

  for (let i = 0; i < args.length; i += 1) {
    const arg = args[i];
    if (arg === '--bump' || arg === '-b') {
      const next = args[i + 1];
      if (!next) {
        throw new Error('Missing value for --bump');
      }
      bump = next;
      i += 1;
    } else if (arg === '--dry-run') {
      dryRun = true;
    } else {
      throw new Error(`Unknown argument: ${arg}`);
    }
  }

  if (!['major', 'minor', 'patch'].includes(bump)) {
    throw new Error(`Unsupported bump type: ${bump}`);
  }

  return { bump, dryRun };
}

function parseVersion(version) {
  const match = /^(\d+)\.(\d+)\.(\d+)$/.exec(version);
  if (!match) {
    throw new Error(`Invalid semver: ${version}`);
  }

  return {
    major: Number.parseInt(match[1], 10),
    minor: Number.parseInt(match[2], 10),
    patch: Number.parseInt(match[3], 10)
  };
}

function compareVersions(a, b) {
  if (a.major !== b.major) {
    return a.major - b.major;
  }

  if (a.minor !== b.minor) {
    return a.minor - b.minor;
  }

  return a.patch - b.patch;
}

function formatVersion(parts) {
  return `${parts.major}.${parts.minor}.${parts.patch}`;
}

function bumpVersion(parts, bump) {
  switch (bump) {
    case 'major':
      return { major: parts.major + 1, minor: 0, patch: 0 };
    case 'minor':
      return { major: parts.major, minor: parts.minor + 1, patch: 0 };
    default:
      return { major: parts.major, minor: parts.minor, patch: parts.patch + 1 };
  }
}

async function readJson(relativePath) {
  const absolute = path.resolve(relativePath);
  const content = await fs.readFile(absolute, 'utf8');
  return { absolute, data: JSON.parse(content) };
}

async function writeJson(absolute, data) {
  const content = `${JSON.stringify(data, null, 2)}\n`;
  await fs.writeFile(absolute, content, 'utf8');
}

async function updatePackageVersions(newVersion, dryRun) {
  for (const packagePath of PACKAGE_PATHS) {
    const { absolute, data } = await readJson(packagePath);
    data.version = newVersion;
    if (!dryRun) {
      await writeJson(absolute, data);
    }
  }

  for (const lockPath of LOCK_PATHS) {
    const { absolute, data } = await readJson(lockPath);
    data.version = newVersion;
    if (data.packages && data.packages['']) {
      data.packages[''].version = newVersion;
    }

    if (!dryRun) {
      await writeJson(absolute, data);
    }
  }
}

async function main() {
  const { bump, dryRun } = parseArguments();

  const versions = [];
  for (const packagePath of PACKAGE_PATHS) {
    const { data } = await readJson(packagePath);
    versions.push(parseVersion(data.version));
  }

  const highest = versions.reduce((max, current) =>
    compareVersions(max, current) >= 0 ? max : current
  );

  const bumped = bumpVersion(highest, bump);
  const newVersion = formatVersion(bumped);

  await updatePackageVersions(newVersion, dryRun);

  console.log(newVersion);
}

main().catch(error => {
  console.error(error.message);
  process.exit(1);
});
