// 2026-11-22-66e4a: what each model deployment has left in the answering server process — read from memory, no
// provider call. Each replica counts its own calls, so the answer names its replica.

import { getJson } from "@/lib/apiResponse";

export interface DeploymentQuota {
  provider: string;
  deployment: string;
  tokensPerMinute: number;
  requestsPerMinute: number;
  freeTokens: number;
  freeRequests: number;
  /** Until both budgets are full again, debt included. */
  secondsToFull: number;
  /** How long a provider refusal still pauses every caller. */
  pausedSeconds: number;
  /** Calls queued for the deployment now. */
  waiting: number;
  providerRemainingTokens: number | null;
  providerRemainingRequests: number | null;
  providerReadAt: string | null;
}

export interface ModelQuota {
  replica: string;
  deployments: DeploymentQuota[];
}

export async function fetchModelQuota(signal?: AbortSignal): Promise<ModelQuota> {
  return getJson<ModelQuota>(`/api/diagnostics/model-quota`, signal);
}
