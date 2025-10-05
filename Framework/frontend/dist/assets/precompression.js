import { createReadStream, createWriteStream } from 'node:fs';
import { constants as zlibConstants, createBrotliCompress, createGzip } from 'node:zlib';
export async function createCompressedVariants(filePath) {
    await Promise.all([
        compress(filePath, '.br', () => createBrotliCompress({ params: { [zlibConstants.BROTLI_PARAM_QUALITY]: 11 } })),
        compress(filePath, '.gz', () => createGzip({ level: zlibConstants.Z_BEST_COMPRESSION }))
    ]);
}
async function compress(source, extension, factory) {
    return new Promise((resolve, reject) => {
        const destination = `${source}${extension}`;
        const readStream = createReadStream(source);
        const writeStream = createWriteStream(destination);
        const compressor = factory();
        readStream.on('error', reject);
        writeStream.on('error', reject);
        compressor.on('error', reject);
        writeStream.on('close', resolve);
        readStream.pipe(compressor).pipe(writeStream);
    });
}
