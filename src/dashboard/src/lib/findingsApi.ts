// p0391a: what is wrong with this installation. The server always starts — a missing
// database, an unreachable Redis or a trigger that cannot park is a finding, not an exit
// code — so the dashboard is reachable exactly when there is something to report, and this
// is where it reads it.

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "";

export interface StartupFinding {
  subsystem: string;
  /** "blocking" — the named unit is not running; "advisory" — it runs, but know this. */
  severity: string;
  reason: string;
  project: string | null;
  trigger: string | null;
  field: string | null;
}

export interface StartupFindings {
  degraded: boolean;
  blocking: number;
  advisory: number;
  findings: StartupFinding[];
}

export async function fetchFindings(signal?: AbortSignal): Promise<StartupFindings> {
  const res = await fetch(`${API_BASE}/api/config/findings`, { signal });
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return (await res.json()) as StartupFindings;
}
