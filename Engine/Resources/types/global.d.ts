declare module '*.module.css' {
  const classes: Record<string, string>;
  export default classes;
}

declare module '*.css' {
  const css: string;
  export default css;
}

declare module '@webstir-io/webstir-testing' {
  export function test(description: string, callback: () => void | Promise<void>): void;
  export const assert: {
    isTrue(condition: unknown, message?: string): asserts condition;
    equal<T>(actual: T, expected: T, message?: string): void;
    fail(message?: string): never;
  };
  export function run(): Promise<void>;
}

declare module 'node:fs' {
  export function readFileSync(path: string, options?: { encoding?: string } | string): string;
}

declare module 'node:path' {
  export function dirname(path: string): string;
  export function resolve(...segments: string[]): string;
}

declare module 'node:url' {
  export function fileURLToPath(url: string): string;
}
