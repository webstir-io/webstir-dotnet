import type { CheerioAPI } from 'cheerio';

interface LazyOptions {
    readonly skip: number;
}

const DEFAULT_OPTIONS: LazyOptions = {
    skip: 1
};

export function applyLazyLoading(document: CheerioAPI, options: LazyOptions = DEFAULT_OPTIONS): void {
    const { skip } = options;
    let index = 0;
    document('img').each((_i, element) => {
        const img = document(element);
        if (img.attr('loading')) {
            return;
        }

        index += 1;
        if (index <= skip) {
            return;
        }

        img.attr('loading', 'lazy');
        if (!img.attr('fetchpriority')) {
            img.attr('fetchpriority', 'low');
        }
    });
}
