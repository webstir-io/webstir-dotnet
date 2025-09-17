"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.createBuilders = createBuilders;
const cssBuilder_js_1 = require("./cssBuilder.js");
const htmlBuilder_js_1 = require("./htmlBuilder.js");
const jsBuilder_js_1 = require("./jsBuilder.js");
const staticAssetsBuilder_js_1 = require("./staticAssetsBuilder.js");
function createBuilders(context) {
    return [
        (0, jsBuilder_js_1.createJavaScriptBuilder)(context),
        (0, cssBuilder_js_1.createCssBuilder)(context),
        (0, htmlBuilder_js_1.createHtmlBuilder)(context),
        (0, staticAssetsBuilder_js_1.createStaticAssetsBuilder)(context)
    ];
}
