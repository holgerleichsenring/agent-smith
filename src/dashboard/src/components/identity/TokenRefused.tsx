"use client";

import type { AuthRequirements, TokenRefusal } from "@/lib/authRequirementsApi";
import { useRuntimeSettings } from "@/lib/runtimeSettings/RuntimeSettingsProvider";
import { recogniseShape } from "./recogniseShape";

// 2026-08-25-1806: the server did not accept your token, and which check refused it.
//
// A refused token resolves to an anonymous caller, which rendered against every field as
// "nothing arrived" — exactly what an ACCEPTED token carrying no role renders as. The two
// have opposite remedies: one is a mapping to write, the other is an audience or an issuer
// that does not match, and an operator reading the wrong one loses an afternoon.
//
// The server hands over a CLASSIFICATION, never the validation message: the message names
// the values the check ran against, and the route that carries this answers anybody.
//
// 2026-09-14-c72e: it now also hands over what THIS caller's token carried, so the page
// shows both sides of the comparison instead of half of it. Naming the check that failed
// without naming the value that failed it sent an operator through a session of decoding
// tokens by hand to learn something the server had already read.
//
// The audience is a true expected-versus-presented pair. The AUTHORITY is not: the server
// validates against the issuer its authority's discovery document names, which for one
// directory's two endpoints is a different string from the authority itself — so the two
// issuer lines are shown as what each side says, never as a matched pair that must agree.

export function TokenRefused({ requirements }: { requirements: AuthRequirements }) {
  const { auth } = useRuntimeSettings();
  const refusal = requirements.tokenRefusal;
  if (!refusal) return null;
  const recognition = recogniseShape(requirements, auth.clientId.trim());
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
      <dl className="text-sm" data-testid="identity-refusal-comparison">
        <Line label="Audience this server expects" value={requirements.audience} />
        <Line
          label="Audience your token carried"
          value={requirements.presentedAudience}
          testId="presented-audience"
          absent="— not readable from what arrived"
        />
        <Line label="Authority this server validates against" value={requirements.authority} />
        <Line
          label="Issuer your token names"
          value={requirements.presentedIssuer}
          testId="presented-issuer"
          absent="— not readable from what arrived"
        />
        {requirements.presentedTokenVersion && (
          <Line
            label="Token version your token declares"
            value={requirements.presentedTokenVersion}
            testId="presented-version"
          />
        )}
      </dl>
      {recognition && (
        <div className="space-y-2" data-testid="identity-refusal-recognition">
          <p className="text-sm">{recognition.shape}</p>
          <ul className="list-disc space-y-1 pl-5 text-sm text-[var(--color-ink-mid)]">
            {recognition.waysOut.map((way) => (
              <li key={way}>{way}</li>
            ))}
          </ul>
        </div>
      )}
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

function Line({
  label,
  value,
  testId,
  absent = "— none configured",
}: {
  label: string;
  value: string | null;
  testId?: string;
  absent?: string;
}) {
  return (
    <div className="mt-1 flex gap-2">
      <dt className="text-[var(--color-ink-mid)]">{label}</dt>
      <dd className="font-mono" data-testid={testId}>
        {value ?? absent}
      </dd>
    </div>
  );
}
