// p0292: client for the ACTIVE connectivity surface. The GET snapshot lists the
// probeable connections + the webhook panel WITHOUT any outbound call (probing is
// on demand); probeConnection() runs one read-only round-trip when the operator
// clicks Test. "Test all" is a client-side fan-out over probeConnection.

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "";

export interface ConnectionDescriptor {
  name: string;
  type: string;
  /** "repo" | "tracker" */
  kind: string;
}

export interface ConnectionStatus {
  name: string;
  type: string;
  kind: string;
  ok: boolean;
  latencyMs: number;
  error: string | null;
}

export interface WebhookStatus {
  platform: string;
  secretConfigured: boolean;
  lastReceivedUtc: string | null;
}

export interface ConnectionDiagnostics {
  connections: ConnectionDescriptor[];
  webhooks: WebhookStatus[];
}

export async function fetchConnections(signal?: AbortSignal): Promise<ConnectionDiagnostics> {
  const res = await fetch(`${API_BASE}/api/diagnostics/connections`, { signal });
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return (await res.json()) as ConnectionDiagnostics;
}

export async function probeConnection(name: string, signal?: AbortSignal): Promise<ConnectionStatus> {
  const res = await fetch(
    `${API_BASE}/api/diagnostics/connections/${encodeURIComponent(name)}/probe`,
    { method: "POST", signal },
  );
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return (await res.json()) as ConnectionStatus;
}
