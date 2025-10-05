import fs from 'fs-extra';

export async function ensureDir(path: string): Promise<void> {
    await fs.ensureDir(path);
}

export async function emptyDir(path: string): Promise<void> {
    await fs.emptyDir(path);
}

export async function remove(path: string): Promise<void> {
    await fs.remove(path);
}

export async function copy(source: string, destination: string): Promise<void> {
    await fs.copy(source, destination, { overwrite: true, errorOnExist: false });
}

export async function pathExists(path: string): Promise<boolean> {
    return fs.pathExists(path);
}

export async function stat(path: string): Promise<fs.Stats> {
    return fs.stat(path);
}

export async function readJson<T>(path: string): Promise<T | null> {
    try {
        return await fs.readJson(path);
    } catch (error) {
        if ((error as NodeJS.ErrnoException).code === 'ENOENT') {
            return null;
        }
        throw error;
    }
}

export async function writeJson(path: string, data: unknown): Promise<void> {
    await fs.writeJson(path, data, { spaces: 2 });
}

export async function readFile(path: string): Promise<string> {
    return fs.readFile(path, 'utf8');
}

export async function writeFile(path: string, contents: string): Promise<void> {
    await fs.outputFile(path, contents, 'utf8');
}
