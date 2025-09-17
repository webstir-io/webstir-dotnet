"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.hashContent = hashContent;
const node_crypto_1 = require("node:crypto");
function hashContent(content, length = 8) {
    const hash = (0, node_crypto_1.createHash)('sha256').update(content).digest('hex');
    return hash.slice(0, length);
}
