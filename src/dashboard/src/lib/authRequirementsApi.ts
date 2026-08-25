// 2026-08-25-4530: the server's half of "is this installation configured". The
// dashboard holds the other half in its runtime settings and is the only place
// that holds both — the server never learns what the browser was given, and the
// browser never learns what the server demands, so neither can diagnose a
// half-configured installation alone.
//
// The route is anonymous, which is the point rather than a concession: a caller
// with no token is exactly the one who needs this answer.

import { getJson } from "@/lib/apiResponse";

export interface AuthRequirements {
  /** What the server DOES, not what its switch says — enforcement with no
   *  authority configured refuses nobody, and reports false here. */
  enforced: boolean;
  /** The issuer tokens are validated against. Null when none is configured. */
  authority: string | null;
  audience: string | null;
}

export async function fetchAuthRequirements(signal?: AbortSignal): Promise<AuthRequirements> {
  return getJson<AuthRequirements>("/api/auth/requirements", signal);
}
