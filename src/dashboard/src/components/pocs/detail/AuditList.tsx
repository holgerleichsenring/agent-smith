"use client";

import Link from "next/link";
import { useT } from "@/hooks/useT";
import type { TextKey } from "@/lib/i18n/translate";
import { pocRolloutPagePath } from "@/lib/pocRevisionPath";
import type { PocAuditEntry } from "@/lib/pocsApi";

// 2026-11-23-c81ed: every act on the PoC, oldest first — when, who, what, the reason given, and the Rollout page of
// the run that did it. An act this dashboard does not know reads as itself.

const ACTS: Record<string, TextKey> = {
  imported: "pocs.detail.audit.imported",
  overruled: "pocs.detail.audit.overruled",
  deployed: "pocs.detail.audit.deployed",
  stopped: "pocs.detail.audit.stopped",
  started: "pocs.detail.audit.started",
  "rolled-back": "pocs.detail.audit.rolledBack",
  deleted: "pocs.detail.audit.deleted",
  extended: "pocs.detail.audit.extended",
  expired: "pocs.detail.audit.expired",
};

function when(iso: string): string {
  const at = new Date(iso);
  return Number.isNaN(at.getTime()) ? iso : at.toLocaleString();
}

export function AuditList({ pocId, audit }: { pocId: number; audit: PocAuditEntry[] }) {
  const t = useT();
  return (
    <section className="poc-box" data-testid="poc-detail-audit">
      <h2>{t("pocs.detail.audit.title")}</h2>
      {audit.length === 0 ? <p className="poc-meta">{t("pocs.detail.audit.none")}</p> : (
        <ol className="poc-audit">
          {audit.map((entry, i) => {
            const label = ACTS[entry.act];
            return (
              <li key={`${entry.at}-${i}`} data-testid={`poc-detail-audit-${i}`} data-act={entry.act}>
                <span className="mono poc-meta">{when(entry.at)}</span>
                <span><b>{label ? t(label) : entry.act}</b> · {entry.actor}</span>
                {entry.reason && <span className="poc-audit-reason">{entry.reason}</span>}
                {entry.runId && (
                  <Link href={pocRolloutPagePath(pocId, entry.runId)} data-testid={`poc-detail-audit-${i}-run`}>
                    {t("pocs.rollout.title")} →
                  </Link>
                )}
              </li>
            );
          })}
        </ol>
      )}
    </section>
  );
}
