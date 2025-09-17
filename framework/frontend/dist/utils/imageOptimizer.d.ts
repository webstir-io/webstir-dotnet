export interface ImageDimensions {
    width: number;
    height: number;
}
export declare function optimizeImages(sourceDir: string, destinationDir: string): Promise<void>;
export declare function getImageDimensions(filePath: string): Promise<ImageDimensions | null>;
