// 2026-08-25-4530: what the server made of this caller's token. p0503d built the
// endpoint for the caller who holds NOTHING — the first login of an installation
// that has just configured an authority, where no mapping exists yet and the only
// way to write one is to read the values the directory actually sent.
//
// The shape mirrors the server's CallerIdentity record on the wire (camelCase),
// including the claim NAMES: a role that resolved to nothing is indistinguishable
// from a claim nobody looked in, unless the page says which claim was read.

import { getJson } from "@/lib/apiResponse";

export interface CallerIdentity {
  authenticated: boolean;
  subject: string | null;
  issuer: string | null;
  /** The claim the server read role names out of. */
  roleClaim: string;
  /** The claim the server read group values out of. */
  groupClaim: string;
  roleClaimValues: string[];
  groupClaimValues: string[];
  roles: string[];
  permissions: string[];
  /** What the server noticed while resolving — an unknown permission, an overage. */
  findings: string[];
}

export async function fetchIdentity(signal?: AbortSignal): Promise<CallerIdentity> {
  return getJson<CallerIdentity>("/api/identity", signal);
}
