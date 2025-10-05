import type { CheerioAPI } from 'cheerio';
export declare function inlineCriticalCss(document: CheerioAPI, pageName: string, distRoot: string, cssFile?: string): Promise<void>;
