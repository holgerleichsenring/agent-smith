"use client";

import Link from "next/link";
import { useT } from "@/hooks/useT";
import { pocRevisionPagePath } from "@/lib/pocRevisionPath";
import type { PocRow } from "@/lib/pocsApi";
import { runPath } from "@/lib/runPath";
import { pocDot } from "./pocStates";
import { PocStateLabel } from "./PocStateLabel";

// 2026-11-23-933da: one PoC as a card — its name, its address, its state with a dot, who owns it and its last run.
// The detail page arrives with the lifecycle acts (2026-11-23-c81ed), so the card itself links nowhere yet.
// 2026-11-23-933dc: a blocked PoC links to its latest revision's page, which says why.

export function PocCard({ poc }: { poc: PocRow }) {
  const t = useT();
  return (
    <article className="poc-card" data-testid={`poc-card-${poc.id}`} data-state={poc.state}>
      <div className="poc-shot" aria-hidden="true"><i /><i /><span /></div>
      <div className="poc-body">
        <h2 className="poc-name">{poc.name}</h2>
        <div className="poc-host">{poc.host ?? t("pocs.card.noHost")}</div>
        <div className="poc-state">
          <span className={`poc-dot ${pocDot(poc.state)}`} aria-hidden="true" />
          <PocStateLabel state={poc.state} />
          <span className="poc-owner">{t("pocs.card.owner", { owner: poc.ownerName })}</span>
        </div>
        {poc.state === "blocked" && poc.latestRevision != null && (
          <Link className="poc-why" href={pocRevisionPagePath(poc.id, poc.latestRevision)} data-testid={`poc-card-${poc.id}-why`}>
            {t("pocs.card.whyBlocked")} →
          </Link>
        )}
        <div className="poc-foot">
          <span className="poc-project">{poc.project}</span>
          {poc.lastRunId
            ? <Link href={runPath(poc.lastRunId)} data-testid={`poc-card-${poc.id}-run`}>{t("pocs.card.lastRun")} →</Link>
            : <span>{t("pocs.card.noRun")}</span>}
        </div>
      </div>
    </article>
  );
}
