"use client";

import Link from "next/link";
import type { MergedPullRequestItem } from "@/lib/deploymentsApi";
import { ticketPath } from "@/lib/tickets/ticketAddress";
import { useT } from "@/hooks/useT";
import { useAgo } from "./useAgo";

// 2026-11-23-8df6a: the merged pull requests, as the Pull requests page draws its rows — repo and ticket, the pull
// request out to its host, the branch it went into, the commit the merge became, when, and its run.

const SHORT_SHA = 7;

export function MergedSection({ merged }: { merged: MergedPullRequestItem[] }) {
  const t = useT();
  if (merged.length === 0) return null;
  return (
    <section data-testid="deployments-merged" className="scroll-mt-6">
      <div className="section-head">
        <h2>{t("deployments.merged.title")}</h2>
        <span className="cnt" data-testid="deployments-merged-count">{merged.length}</span>
        <span className="sh-sub">{t("deployments.merged.sub")}</span>
      </div>
      <div style={{ height: 14 }} />
      <div className="rows">
        {merged.map((m) => <MergedRow key={m.url} item={m} />)}
      </div>
    </section>
  );
}

function MergedRow({ item }: { item: MergedPullRequestItem }) {
  const t = useT();
  const ago = useAgo();
  const id = `deployments-merged-${item.runId}-${item.number}`;
  return (
    <div className="rrow st-ok" data-testid={id}
      style={{ cursor: "default", gridTemplateColumns: "14px minmax(0, 1fr) auto auto auto auto" }}>
      <span className="sd" />
      <div className="rmain">
        <div className="rt">
          <span className="tick">{item.repo}</span>
          <Link className="ttl" href={ticketPath(item.project, item.ticketId)} data-testid={`${id}-ticket`}>
            {item.ticketTitle ?? `#${item.ticketId}`}
          </Link>
        </div>
        <div className="act">
          <b>#{item.ticketId}</b>
          {item.targetBranch && <> · {t("deployments.merged.into", { branch: item.targetBranch })}</>}
        </div>
      </div>
      {item.mergeSha
        ? <span className="pill ok mono" title={item.mergeSha} data-testid={`${id}-sha`}>{item.mergeSha.slice(0, SHORT_SHA)}</span>
        : <span className="pill q" title={t("deployments.merged.notReadTip")} data-testid={`${id}-sha`}>{t("deployments.merged.notRead")}</span>}
      <span className="prog hidesm">{ago(item.mergedAt)}</span>
      <a className="prog" href={item.url} target="_blank" rel="noreferrer" data-testid={`${id}-link`}>#{item.number} ↗</a>
      <Link className="prog" href={`/jobs/${encodeURIComponent(item.runId)}`} data-testid={`${id}-run`} style={{ cursor: "pointer" }}>
        {t("deployments.run")}
      </Link>
    </div>
  );
}
