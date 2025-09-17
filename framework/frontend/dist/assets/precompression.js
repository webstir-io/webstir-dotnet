"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.createCompressedVariants = createCompressedVariants;
const node_fs_1 = require("node:fs");
const node_zlib_1 = require("node:zlib");
async function createCompressedVariants(filePath) {
    await Promise.all([
        compress(filePath, '.br', () => (0, node_zlib_1.createBrotliCompress)({ params: { [node_zlib_1.constants.BROTLI_PARAM_QUALITY]: 11 } })),
        compress(filePath, '.gz', () => (0, node_zlib_1.createGzip)({ level: node_zlib_1.constants.Z_BEST_COMPRESSION }))
    ]);
}
async function compress(source, extension, factory) {
    return new Promise((resolve, reject) => {
        const destination = `${source}${extension}`;
        const readStream = (0, node_fs_1.createReadStream)(source);
        const writeStream = (0, node_fs_1.createWriteStream)(destination);
        const compressor = factory();
        readStream.on('error', reject);
        writeStream.on('error', reject);
        compressor.on('error', reject);
        writeStream.on('close', resolve);
        readStream.pipe(compressor).pipe(writeStream);
    });
}
