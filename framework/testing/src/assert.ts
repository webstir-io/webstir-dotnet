export class AssertionError extends Error {
  public readonly isAssertionError = true;

  public constructor(message = 'Assertion failed') {
    super(message);
    this.name = 'AssertionError';
  }
}

export function fail(message: string): never {
  throw new AssertionError(String(message));
}

export function isTrue(value: unknown, message?: string): void {
  if (value) {
    return;
  }

  fail(message ?? `Expected truthy value but received ${String(value)}`);
}

export function equal<T>(expected: T, actual: T, message?: string): void {
  if (Object.is(expected, actual)) {
    return;
  }

  const errorMessage = message ?? `Expected ${stringify(expected)} but received ${stringify(actual)}`;
  fail(errorMessage);
}

function stringify(value: unknown): string {
  try {
    return JSON.stringify(value);
  } catch {
    return String(value);
  }
}

export const assert = Object.freeze({ isTrue, equal, fail });
export type AssertApi = typeof assert;
