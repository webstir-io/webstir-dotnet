import type { MetricsConfig } from '../env.js';

export interface RequestMetricsEvent {
  method: string;
  route: string;
  status: number;
  durationMs: number;
}

export interface MetricsSnapshot {
  enabled: true;
  totalRequests: number;
  errorCount: number;
  averageDurationMs: number;
  p95DurationMs: number;
  byStatus: Record<string, number>;
  windowSize: number;
}

export interface MetricsTracker {
  record(event: RequestMetricsEvent): void;
  snapshot(): MetricsSnapshot | undefined;
}

export function createMetricsTracker(config: MetricsConfig): MetricsTracker {
  if (!config.enabled) {
    return {
      record() {
        // no-op
      },
      snapshot() {
        return undefined;
      }
    };
  }

  let totalRequests = 0;
  let errorCount = 0;
  const byStatus = new Map<number, number>();
  const durations: number[] = [];

  return {
    record(event) {
      totalRequests++;
      if (event.status >= 500) {
        errorCount++;
      }
      byStatus.set(event.status, (byStatus.get(event.status) ?? 0) + 1);
      durations.push(event.durationMs);
      if (durations.length > config.windowSize) {
        durations.shift();
      }
    },
    snapshot() {
      const counts = Object.fromEntries(
        [...byStatus.entries()].map(([status, count]) => [String(status), count])
      );
      const averageDurationMs = durations.length > 0 ? durations.reduce((sum, value) => sum + value, 0) / durations.length : 0;
      const p95DurationMs = durations.length > 0 ? percentile(durations, 0.95) : 0;
      return {
        enabled: true,
        totalRequests,
        errorCount,
        averageDurationMs,
        p95DurationMs,
        byStatus: counts,
        windowSize: config.windowSize
      };
    }
  };
}

function percentile(values: readonly number[], ratio: number): number {
  if (values.length === 0) return 0;
  const sorted = [...values].sort((a, b) => a - b);
  const index = Math.min(sorted.length - 1, Math.floor(ratio * (sorted.length - 1)));
  return sorted[index];
}
