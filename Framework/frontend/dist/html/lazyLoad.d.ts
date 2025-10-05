import type { CheerioAPI } from 'cheerio';
interface LazyOptions {
    readonly skip: number;
}
export declare function applyLazyLoading(document: CheerioAPI, options?: LazyOptions): void;
export {};
