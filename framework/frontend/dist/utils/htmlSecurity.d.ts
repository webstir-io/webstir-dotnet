import type { CheerioAPI } from 'cheerio';
export interface SubresourceIntegrityResult {
    readonly failures: string[];
}
export declare function addSubresourceIntegrity(document: CheerioAPI): Promise<SubresourceIntegrityResult>;
