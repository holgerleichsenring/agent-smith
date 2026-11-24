"use client";

import Link from "next/link";
import { useState } from "react";
import { useT } from "@/hooks/useT";
import { ApiRefusal, ApiResponseError } from "@/lib/apiResponse";
import type { T } from "@/lib/i18n/translate";
import { overrulePocRevision, type PocOverruled } from "@/lib/pocsApi";
import { runPath } from "@/lib/runPath";

// 2026-11-23-933dc: lifting a vulnerability block — a reason, then the run that scans the revision again and commits
// it. Shown only to a caller the server said may overrule; a refusal is shown as the server worded it.

/** The server refuses a longer reason (PocRevisionEndpoints); the counter shows how much is left. */
export const OVERRULE_REASON_MAX = 500;

export function OverruleBox({ pocId, revision }: { pocId: number; revision: number }) {
  const t = useT();
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [refusal, setRefusal] = useState<string | null>(null);
  const [started, setStarted] = useState<PocOverruled | null>(null);

  if (started) {
    return (
      <section className="poc-box" data-testid="poc-overrule-started">
        <h2>{t("pocs.revision.overrule.title")}</h2>
        <p>{t("pocs.revision.overrule.started", { revision: started.revision })}</p>
        <Link className="btn primary" href={runPath(started.runId)} data-testid="poc-overrule-run">
          {t("pocs.import.started.run")} →
        </Link>
      </section>
    );
  }

  const submit = async () => {
    setBusy(true);
    setRefusal(null);
    try {
      setStarted(await overrulePocRevision(pocId, revision, reason.trim()));
    } catch (err) {
      setRefusal(overruleRefusal(t, err));
    } finally {
      setBusy(false);
    }
  };

  const id = "poc-overrule-reason";
  return (
    <section className="poc-box poc-overrule" data-testid="poc-overrule">
      <h2>{t("pocs.revision.overrule.title")}</h2>
      <div className="poc-meta">{t("pocs.revision.overrule.help")}</div>
      <label className="poc-meta" htmlFor={id}>{t("pocs.revision.overrule.label", { revision })}</label>
      <textarea id={id} value={reason} maxLength={OVERRULE_REASON_MAX} data-testid="poc-overrule-reason"
        onChange={(e) => setReason(e.target.value)} />
      <div className="poc-go">
        <button type="button" className="btn" disabled={busy || reason.trim() === ""} onClick={() => void submit()}
          data-testid="poc-overrule-submit">
          {busy ? t("pocs.revision.overrule.submitting") : t("pocs.revision.overrule.submit", { revision })}
        </button>
        <span className="poc-meta" data-testid="poc-overrule-count">
          {t("pocs.revision.overrule.count", { count: reason.length, max: OVERRULE_REASON_MAX })}
        </span>
      </div>
      {refusal && <div className="poc-refusal" role="alert" data-testid="poc-overrule-refusal">{refusal}</div>}
    </section>
  );
}

/** The server's sentence as it came; a refusal that said nothing gets one plain line instead. */
function overruleRefusal(t: T, err: unknown): string {
  if (err instanceof ApiRefusal) return t("pocs.revision.overrule.forbidden");
  if (err instanceof ApiResponseError) {
    if (err.reason) return err.reason;
    return err.status === 404 ? t("pocs.revision.overrule.notFound") : t("pocs.revision.overrule.unsaid", { status: err.status });
  }
  return t("pocs.revision.overrule.failed", { message: err instanceof Error ? err.message : String(err) });
}
