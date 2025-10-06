import type { Builder, BuilderContext } from './types.js';
import { createCssBuilder } from './cssBuilder.js';
import { createHtmlBuilder } from './htmlBuilder.js';
import { createJavaScriptBuilder } from './jsBuilder.js';
import { createStaticAssetsBuilder } from './staticAssetsBuilder.js';

export function createBuilders(context: BuilderContext): Builder[] {
    return [
        createJavaScriptBuilder(context),
        createCssBuilder(context),
        createHtmlBuilder(context),
        createStaticAssetsBuilder(context)
    ];
}
