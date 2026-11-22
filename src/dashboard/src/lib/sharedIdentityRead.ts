import { fetchIdentity, type CallerIdentity } from "@/lib/identityApi";

// 2026-11-22-9719c: one read per caller for the whole page. Every gated act — a Close button on each ticket row —
// asks for the identity, and each used to send its own request: a list of 95 tickets sent about 200. Callers now
// share the read for the same token while it is in flight and for SHARE_MS after; a failed read is not kept.
export const SHARE_MS = 30_000; // not a limit: how long one answer serves every caller on the page
let shared: { token: string | null; at: number; read: Promise<CallerIdentity> } | null = null;

export function sharedIdentityRead(token: string | null): Promise<CallerIdentity> {
  const now = Date.now();
  if (shared && shared.token === token && now - shared.at < SHARE_MS) return shared.read;
  const read = fetchIdentity();
  const entry = { token, at: now, read };
  shared = entry;
  read.catch(() => { if (shared === entry) shared = null; });
  return read;
}

/** Forgets the shared read: the identity page reads afresh after a mapping or binding edit; tests start clean. */
export function resetSharedCallerIdentity(): void {
  shared = null;
}
