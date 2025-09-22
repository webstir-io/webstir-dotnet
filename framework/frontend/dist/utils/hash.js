import { createHash } from 'node:crypto';
export function hashContent(content, length = 8) {
    const hash = createHash('sha256').update(content).digest('hex');
    return hash.slice(0, length);
}
