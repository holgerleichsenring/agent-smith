"use client";

import type { AuthRequirements, TokenRefusal } from "@/lib/authRequirementsApi";

// 2026-08-25-1806: the server did not accept your token, and which check refused it.
//
// A refused token resolves to an anonymous caller, which rendered against every field as
// "nothing arrived" — exactly what an ACCEPTED token carrying no role renders as. The two
// have opposite remedies: one is a mapping to write, the other is an audience or an issuer
// that does not match, and an operator reading the wrong one loses an afternoon.
//
// The server hands over a CLASSIFICATION, never the validation message: the message names
// the values the check ran against, and the route that carries this answers anybody.

export function TokenRefused({ requirements }: { requirements: AuthRequirements }) {
  const refusal = requirements.tokenRefusal;
  if (!refusal) return null;
  return (
    <div className="space-y-3" data-testid="identity-token-refused" data-refusal={refusal}>
      <h2 className="text-xs font-semibold uppercase tracking-wide">
        This server did not accept your token
      </h2>
      <p className="text-sm">{REASON[refusal] ?? REASON.rejected}</p>
      <p className="text-sm text-[var(--color-ink-mid)]">
        No role mapping can change this: a mapping decides what an ACCEPTED token grants, and
        this one was refused before any of it was read.
      </p>
      <dl className="text-sm">
        <Expectation label="Authority" value={requirements.authority} />
        <Expectation label="Audience" value={requirements.audience} />
      </dl>
    </div>
  );
}

const REASON: Record<TokenRefusal, string> = {
  expired: "Your token has expired. Sign out and in again; if it happens immediately every time, the token's lifetime is shorter than the clock skew between the two machines.",
  not_yet_valid: "Your token is not valid yet, which means this server's clock and your identity provider's disagree. Check the time on both.",
  audience: "Your token was not issued for the audience this server accepts. The dashboard is asking your authority for a token for a different audience than the server was configured with.",
  issuer: "Your token was issued by a different authority than the one this server validates against. One of the two authorities is wrong — they are shown below.",
  signature: "Your token's signature did not verify against the keys this authority publishes. The authority's signing keys have rotated, or the token did not come from it.",
  malformed: "What arrived was not a well-formed token, so nothing in it could be read.",
  rejected: "Your token was refused, and the check that refused it is not one this server names separately. The server's log carries the detail.",
};

function Expectation({ label, value }: { label: string; value: string | null }) {
  return (
    <div className="mt-1 flex gap-2">
      <dt className="text-[var(--color-ink-mid)]">{label} this server expects</dt>
      <dd className="font-mono">{value ?? "— none configured"}</dd>
    </div>
  );
}
