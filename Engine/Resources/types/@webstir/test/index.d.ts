export {};

declare module '@webstir/test' {
  type TestCallback = () => unknown | Promise<unknown>;
  export function test(name: string, fn?: TestCallback): void;
  export const assert: {
    isTrue(value: unknown, message?: string): void;
    equal<T>(expected: T, actual: T, message?: string): void;
    fail(message: string): never;
  };
}

declare module '*.module.css' {
  const classes: Record<string, string>;
  export default classes;
}

declare module '*.css' {
  const css: string;
  export default css;
}
