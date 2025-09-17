"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.createPageScaffold = createPageScaffold;
const node_path_1 = __importDefault(require("node:path"));
const constants_js_1 = require("./constants.js");
const fs_js_1 = require("./fs.js");
async function createPageScaffold(options) {
    const pageDir = node_path_1.default.join(options.paths.pages, options.pageName);
    if (await (0, fs_js_1.pathExists)(pageDir)) {
        throw new Error(`Page '${options.pageName}' already exists.`);
    }
    await (0, fs_js_1.ensureDir)(pageDir);
    await Promise.all([
        (0, fs_js_1.writeFile)(node_path_1.default.join(pageDir, `${constants_js_1.FILES.index}${constants_js_1.EXTENSIONS.html}`), buildHtmlTemplate(options.pageName)),
        (0, fs_js_1.writeFile)(node_path_1.default.join(pageDir, `${constants_js_1.FILES.index}${constants_js_1.EXTENSIONS.css}`), buildCssTemplate(options.pageName)),
        (0, fs_js_1.writeFile)(node_path_1.default.join(pageDir, `${constants_js_1.FILES.index}${constants_js_1.EXTENSIONS.ts}`), buildScriptTemplate())
    ]);
}
function buildHtmlTemplate(pageName) {
    return `<head>
    <meta charset="utf-8">
    <title>${pageName}</title>
    <link rel="stylesheet" href="${constants_js_1.FILES.index}${constants_js_1.EXTENSIONS.css}">
</head>
<body>
    <main>
        <h1>${pageName}</h1>
        <p>Content for the ${pageName} page.</p>
    </main>
    <script type="module" src="${constants_js_1.FILES.index}${constants_js_1.EXTENSIONS.js}" async></script>
</body>
`;
}
function buildCssTemplate(pageName) {
    return `/* ${pageName} Page Styles */
@import "@app/app.css";

/* Add your page-specific styles here */
`;
}
function buildScriptTemplate() {
    return `// Page entry point
import '../../app/app';

// Add page-specific logic here
`;
}
