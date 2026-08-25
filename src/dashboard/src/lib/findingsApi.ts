// p0391a: what is wrong with this installation. The server always starts — a missing
// database, an unreachable Redis or a trigger that cannot park is a finding, not an exit
// code — so the dashboard is reachable exactly when there is something to report, and this
// is where it reads it.

import { getJson } from "@/lib/apiResponse";
import { BUILD_REVISION } from "@/lib/buildIdentity";

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

// 2026-08-25-8c97: the request names the build this bundle came from, so the server can
// say whether it is running a different one. It travels on the findings request rather
// than as a header on every call: a header would mean editing every call site, and the
// hub could not carry one at all, because a browser websocket cannot set request headers.
// A bundle built without a stamped revision sends nothing and is told nothing.
export async function fetchFindings(signal?: AbortSignal): Promise<StartupFindings> {
  const query = BUILD_REVISION ? `?build=${encodeURIComponent(BUILD_REVISION)}` : "";
  return getJson<StartupFindings>(`/api/config/findings${query}`, signal);
}
