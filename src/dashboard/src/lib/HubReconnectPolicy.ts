import { IRetryPolicy, RetryContext } from "@microsoft/signalr";

// p0169f: 0 / 2 / 10 / 30 second backoff, then 30s steady. Matches the
// SignalR default retry policy shape, kept explicit so we can tweak it
// when operator feedback says "reconnect is too eager / too slow".
const SCHEDULE_SECONDS = [0, 2, 10, 30];

export class HubReconnectPolicy implements IRetryPolicy {
  nextRetryDelayInMilliseconds(retryContext: RetryContext): number | null {
    const index = retryContext.previousRetryCount;
    const seconds = SCHEDULE_SECONDS[index] ?? SCHEDULE_SECONDS[SCHEDULE_SECONDS.length - 1];
    return seconds * 1000;
  }
}
