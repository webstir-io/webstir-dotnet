import path from 'node:path';
import fs from 'fs-extra';

import { TestManifest, TestModule, TestRuntime } from './types.js';

const SRC_FOLDER = 'src';
const TEST_FOLDER = 'tests';
const BUILD_FOLDER = 'build';
const BACKEND_FOLDER = 'backend';
const EXCLUDED_DIRECTORIES = new Set(['node_modules', 'build', 'dist', '.git']);
const TEST_FILE_SUFFIXES = ['.test.ts', '.test.js'];

export async function discoverTestManifest(workspaceRoot: string): Promise<TestManifest> {
  const absoluteRoot = path.resolve(workspaceRoot);
  const srcRoot = path.join(absoluteRoot, SRC_FOLDER);

  const exists = await fs.pathExists(srcRoot);
  if (!exists) {
    return {
      workspaceRoot: absoluteRoot,
      generatedAt: new Date().toISOString(),
      modules: [],
    } satisfies TestManifest;
  }

  const modules: TestModule[] = [];
  await walkDirectory(srcRoot, async (filePath) => {
    const relativeToSrc = path.relative(srcRoot, filePath);
    if (relativeToSrc.startsWith('..')) {
      return;
    }

    if (!isUnderTestsFolder(relativeToSrc)) {
      return;
    }

    if (!isTestFile(filePath)) {
      return;
    }

    const runtime = inferRuntime(relativeToSrc);
    const compiledPath = computeCompiledPath(absoluteRoot, relativeToSrc, runtime);

    modules.push({
      id: normalizeModuleId(relativeToSrc),
      runtime,
      sourcePath: path.resolve(srcRoot, relativeToSrc),
      compiledPath,
    });
  });

  modules.sort((a, b) => a.id.localeCompare(b.id));

  return {
    workspaceRoot: absoluteRoot,
    generatedAt: new Date().toISOString(),
    modules,
  } satisfies TestManifest;
}

async function walkDirectory(root: string, onFile: (filePath: string) => Promise<void>): Promise<void> {
  const entries = await fs.readdir(root, { withFileTypes: true });

  await Promise.all(entries.map(async (entry) => {
    const entryPath = path.join(root, entry.name);

    if (entry.isDirectory()) {
      if (EXCLUDED_DIRECTORIES.has(entry.name) || entry.name.startsWith('.')) {
        return;
      }

      await walkDirectory(entryPath, onFile);
      return;
    }

    if (entry.isFile()) {
      await onFile(entryPath);
    }
  }));
}

function isTestFile(filePath: string): boolean {
  return TEST_FILE_SUFFIXES.some((suffix) => filePath.endsWith(suffix));
}

function isUnderTestsFolder(relativePath: string): boolean {
  const segments = splitPath(relativePath);
  return segments.includes(TEST_FOLDER);
}

function splitPath(relativePath: string): string[] {
  return relativePath.split(/[\\/]+/).filter((segment) => segment.length > 0);
}

function inferRuntime(relativePath: string): TestRuntime {
  const segments = splitPath(relativePath);
  if (segments.length === 0) {
    return 'frontend';
  }

  return segments[0] === BACKEND_FOLDER ? 'backend' : 'frontend';
}

function computeCompiledPath(workspaceRoot: string, relativeToSrc: string, runtime: TestRuntime): string | null {
  const buildRoot = path.join(workspaceRoot, BUILD_FOLDER);
  const compiledRelative = replaceExtension(relativeToSrc, '.js');
  return path.join(buildRoot, compiledRelative);
}

function replaceExtension(relativePath: string, newExtension: string): string {
  const ext = path.extname(relativePath);
  if (!ext) {
    return `${relativePath}${newExtension}`;
  }

  return `${relativePath.slice(0, -ext.length)}${newExtension}`;
}

function normalizeModuleId(relativePath: string): string {
  return splitPath(relativePath).join('/');
}
