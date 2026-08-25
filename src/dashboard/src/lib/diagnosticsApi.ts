// p0292: client for the ACTIVE connectivity surface. The GET snapshot lists the
// probeable connections + the webhook panel WITHOUT any outbound call (probing is
// on demand); probeConnection() runs one read-only round-trip when the operator
// clicks Test. "Test all" is a client-side fan-out over probeConnection.

import { apiFetch, getJson, readJson } from "@/lib/apiResponse";

export interface ConnectionDescriptor {
  name: string;
  type: string;
  /** repo | tracker | agent | redis | persistence | sandbox | chat */
  kind: string;
  /** service | agent | infra | chat — page grouping */
  category: string;
}

export interface ConnectionStatus {
  name: string;
  type: string;
  kind: string;
  category: string;
  ok: boolean;
  latencyMs: number;
  error: string | null;
}

export interface WebhookStatus {
  platform: string;
  secretConfigured: boolean;
  lastReceivedUtc: string | null;
  /** p0506: a delivery reached this deployment and nothing verified that the platform sent it. */
  acceptedUnsignedDelivery: boolean;
}

export interface ConnectionDiagnostics {
  connections: ConnectionDescriptor[];
  webhooks: WebhookStatus[];
}

export async function fetchConnections(signal?: AbortSignal): Promise<ConnectionDiagnostics> {
  return getJson<ConnectionDiagnostics>(`/api/diagnostics/connections`, signal);
}

export async function probeConnection(name: string, signal?: AbortSignal): Promise<ConnectionStatus> {
  const path = `/api/diagnostics/connections/${encodeURIComponent(name)}/probe`;
  return readJson<ConnectionStatus>(await apiFetch(path, { method: "POST", signal }), path);
}
