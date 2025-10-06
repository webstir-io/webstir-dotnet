import fs from 'fs-extra';
export async function ensureDir(path) {
    await fs.ensureDir(path);
}
export async function emptyDir(path) {
    await fs.emptyDir(path);
}
export async function remove(path) {
    await fs.remove(path);
}
export async function copy(source, destination) {
    await fs.copy(source, destination, { overwrite: true, errorOnExist: false });
}
export async function pathExists(path) {
    return fs.pathExists(path);
}
export async function stat(path) {
    return fs.stat(path);
}
export async function readJson(path) {
    try {
        return await fs.readJson(path);
    }
    catch (error) {
        if (error.code === 'ENOENT') {
            return null;
        }
        throw error;
    }
}
export async function writeJson(path, data) {
    await fs.writeJson(path, data, { spaces: 2 });
}
export async function readFile(path) {
    return fs.readFile(path, 'utf8');
}
export async function writeFile(path, contents) {
    await fs.outputFile(path, contents, 'utf8');
}
