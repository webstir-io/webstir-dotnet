export {};

declare global {
  function test(name: string, fn?: () => unknown | Promise<unknown>): void;
  namespace assert {
    function isTrue(value: unknown, message?: string): void;
    function equal<T>(expected: T, actual: T, message?: string): void;
    function fail(message: string): never;
  }
}

