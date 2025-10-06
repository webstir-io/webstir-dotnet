import { RunnerEvent } from './types.js';

export const EVENT_PREFIX = 'WEBSTIR_TEST ';

export function emitEvent(event: RunnerEvent): void {
  process.stdout.write(`${EVENT_PREFIX}${JSON.stringify(event)}\n`);
}

export function createRunId(): string {
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`;
}
