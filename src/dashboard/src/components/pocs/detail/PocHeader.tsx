"use client";

import Link from "next/link";
import { useT } from "@/hooks/useT";
import type { PocDetail } from "@/lib/pocsApi";
import { runPath } from "@/lib/runPath";
import { pocDot } from "../pocStates";
import { PocStateLabel } from "../PocStateLabel";

// 2026-11-23-c81ed: the PoC's name, its address (a link once it has one), its state with its dot, a delete that has
// not settled yet, its owner and its last run.

const POCS_LIST_PATH = "/pocs";

export function PocHeader({ poc }: { poc: PocDetail }) {
  const t = useT();
  return (
    <>
      <div className="poc-crumb"><Link href={POCS_LIST_PATH}>{t("pocs.title")}</Link> / {poc.name}</div>
      <div className="m-head poc-detail-head" data-testid="poc-detail-header">
        <div>
          <h1 className="poc-rev-title">
            {poc.name}
            <span className="poc-detail-state" data-testid="poc-detail-state" data-state={poc.state}>
              <span className={`poc-dot ${pocDot(poc.state)}`} aria-hidden="true" />
              <PocStateLabel state={poc.state} />
            </span>
            {poc.deletePending && (
              <span className="poc-badge run" data-testid="poc-detail-delete-pending">
                <span className="poc-badge-dot" aria-hidden="true" />{t("pocs.detail.deletePending")}
              </span>
            )}
          </h1>
          <div className="msub mono" data-testid="poc-detail-host">
            {poc.url
              ? <a href={poc.url} target="_blank" rel="noreferrer" data-testid="poc-detail-url">{poc.host ?? poc.url} ↗</a>
              : poc.host ?? t("pocs.card.noHost")}
          </div>
          <div className="poc-meta">
            {t("pocs.card.owner", { owner: poc.ownerName })} · <span className="mono">{poc.project}</span>
            {poc.lastRunId && (
              <>{" · "}<Link href={runPath(poc.lastRunId)} data-testid="poc-detail-last-run">{t("pocs.card.lastRun")} →</Link></>
            )}
          </div>
        </div>
      </div>
    </>
  );
}
