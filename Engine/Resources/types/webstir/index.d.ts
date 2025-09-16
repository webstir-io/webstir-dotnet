export {};

declare global {
  function test(name: string, fn?: () => unknown | Promise<unknown>): void;
  namespace assert {
    function isTrue(value: unknown, message?: string): void;
    function equal<T>(expected: T, actual: T, message?: string): void;
    function fail(message: string): never;
  }
}

declare module '*.module.css' {
  const classes: Record<string, string>;
  export default classes;
}

declare module '*.css' {
  const css: string;
  export default css;
}
