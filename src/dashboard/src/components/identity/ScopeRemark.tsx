"use client";

import type { AuthRequirements } from "@/lib/authRequirementsApi";
import { useAccessToken } from "@/hooks/useAccessToken";
import { useRuntimeSettings } from "@/lib/runtimeSettings/RuntimeSettingsProvider";
import { scopeMismatch } from "./scopeAddresses";

// 2026-09-14-d4e8: the one thing a refusal cannot say, because it can only say it
// afterwards — that the scope this dashboard asks for names a different resource than the
// one the server validates.
//
// It stands down wherever something with better evidence is speaking. A token this tab
// HOLDS that was not refused has proven the scopes produce something this server takes,
// whatever the strings look like; a token that was REFUSED is already explained above by
// the comparison 2026-09-14-c72e added, with both sides shown and the shape named. What is
// left is the gap neither covers: before the first sign-in, with no token in hand.

export function ScopeRemark({ requirements }: { requirements: AuthRequirements | null }) {
  const { auth } = useRuntimeSettings();
  const token = useAccessToken();

  // Nothing has arrived yet, or a refusal is being explained: an answer that has not come
  // back is not a mismatch, and a second surface arguing beside the first is noise.
  if (requirements === null || requirements.tokenRefusal !== null) return null;
  // A token in hand that was not refused is proof, and proof outranks this inference.
  if (token !== null) return null;

  const mismatch = scopeMismatch(
    // What the client SENDS, which is not the configured value when that is empty.
    auth.scopes.trim() || "openid",
    auth.audience,
    requirements.audience,
  );
  if (mismatch === null) return null;

  return (
    <div className="space-y-2" data-testid="identity-scope-remark">
      <h2 className="text-xs font-semibold uppercase tracking-wide">
        These scopes name a different resource
      </h2>
      <p className="text-sm">
        This dashboard asks for a token for{" "}
        <span className="font-mono">{mismatch.asked}</span>, and this server validates tokens
        for <span className="font-mono">{mismatch.validated}</span>. A token minted for the
        first will be refused by the second.
      </p>
      <p className="text-sm text-[var(--color-ink-mid)]">
        Point the dashboard&apos;s scopes at this server&apos;s API. Silence here is not a
        promise that sign-in will work — a scope nobody has consented to, and an API minting a
        different token version, both pass this comparison.
      </p>
    </div>
  );
}
