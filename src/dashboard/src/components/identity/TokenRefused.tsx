"use client";

import type { AuthRequirements, TokenRefusal } from "@/lib/authRequirementsApi";
import { useRuntimeSettings } from "@/lib/runtimeSettings/RuntimeSettingsProvider";
import { recogniseShape } from "./recogniseShape";
import { useT } from "@/hooks/useT";
import type { TextKey } from "@/lib/i18n/translate";

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
  const t = useT();
  const { auth } = useRuntimeSettings();
  const refusal = requirements.tokenRefusal;
  if (!refusal) return null;
  const recognition = recogniseShape(requirements, auth.clientId.trim(), t);
  const notReadable = t("identity.refused.notReadable");
  return (
    <div className="space-y-3" data-testid="identity-token-refused" data-refusal={refusal}>
      <h2 className="text-xs font-semibold uppercase tracking-wide">
        {t("identity.refused.title")}
      </h2>
      <p className="text-sm">{t(REASON[refusal] ?? REASON.rejected)}</p>
      <p className="text-sm text-[var(--color-ink-mid)]">{t("identity.refused.noMapping")}</p>
      <dl className="text-sm" data-testid="identity-refusal-comparison">
        <Line label={t("identity.refused.audienceExpected")} value={requirements.audience} />
        <Line
          label={t("identity.refused.audienceCarried")}
          value={requirements.presentedAudience}
          testId="presented-audience"
          absent={notReadable}
        />
        <Line label={t("identity.refused.authority")} value={requirements.authority} />
        <Line
          label={t("identity.refused.issuer")}
          value={requirements.presentedIssuer}
          testId="presented-issuer"
          absent={notReadable}
        />
        {requirements.presentedTokenVersion && (
          <Line
            label={t("identity.refused.version")}
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

const REASON: Record<TokenRefusal, TextKey> = {
  expired: "identity.reason.expired",
  not_yet_valid: "identity.reason.notYetValid",
  audience: "identity.reason.audience",
  issuer: "identity.reason.issuer",
  signature: "identity.reason.signature",
  malformed: "identity.reason.malformed",
  rejected: "identity.reason.rejected",
};

function Line({
  label,
  value,
  testId,
  absent,
}: {
  label: string;
  value: string | null;
  testId?: string;
  absent?: string;
}) {
  const t = useT();
  return (
    <div className="mt-1 flex gap-2">
      <dt className="text-[var(--color-ink-mid)]">{label}</dt>
      <dd className="font-mono" data-testid={testId}>
        {value ?? absent ?? t("identity.refused.noneConfigured")}
      </dd>
    </div>
  );
}
