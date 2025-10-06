import { createCssBuilder } from './cssBuilder.js';
import { createHtmlBuilder } from './htmlBuilder.js';
import { createJavaScriptBuilder } from './jsBuilder.js';
import { createStaticAssetsBuilder } from './staticAssetsBuilder.js';
export function createBuilders(context) {
    return [
        createJavaScriptBuilder(context),
        createCssBuilder(context),
        createHtmlBuilder(context),
        createStaticAssetsBuilder(context)
    ];
}
