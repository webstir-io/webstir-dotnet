#!/usr/bin/env node
const fs = require('fs');
const path = require('path');

if (process.argv.length < 6) {
  console.error('Usage: update-package-manifest <manifestPath> <packageName> <version> <tarballPath> <hash> [registrySpecifier]');
  process.exit(1);
}

const [, , manifestPath, packageName, version, tarballPath, hash, registrySpecifierRaw] = process.argv;
const manifestDir = path.dirname(manifestPath);
const absoluteTarballPath = path.resolve(tarballPath);
const fileName = path.basename(absoluteTarballPath);
const repositoryPath = path.relative(manifestDir, absoluteTarballPath).split(path.sep).join('/');
const registrySpecifier = (registrySpecifierRaw ?? '').trim();

const next = {
  schemaVersion: 1,
  packages: {}
};

if (fs.existsSync(manifestPath)) {
  try {
    const current = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
    if (typeof current.schemaVersion === 'number') {
      next.schemaVersion = current.schemaVersion;
    }
    if (current.packages && typeof current.packages === 'object' && !Array.isArray(current.packages)) {
      next.packages = current.packages;
    }
  } catch (error) {
    console.warn(`Warning: unable to parse existing manifest at ${manifestPath}: ${error.message}`);
  }
}

next.packages[packageName] = {
  name: packageName,
  version,
  fileName,
  dependency: `file:./.tools/${fileName}`,
  hash,
  repositoryPath
};

if (registrySpecifier) {
  next.packages[packageName].registrySpecifier = registrySpecifier;
}

const sortedEntries = Object.keys(next.packages).sort().reduce((acc, key) => {
  acc[key] = next.packages[key];
  return acc;
}, {});

const output = {
  schemaVersion: next.schemaVersion,
  packages: sortedEntries
};

fs.mkdirSync(manifestDir, { recursive: true });
fs.writeFileSync(manifestPath, `${JSON.stringify(output, null, 2)}\n`);
