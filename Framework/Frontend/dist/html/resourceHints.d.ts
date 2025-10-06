import type { CheerioAPI } from 'cheerio';
export interface ResourceHintResult {
    readonly added: number;
    readonly candidates: string[];
    readonly missingHead: boolean;
}
export declare function injectResourceHints(document: CheerioAPI, currentPage: string): ResourceHintResult;
