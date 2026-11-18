"use client";

import { useEffect, useState } from "react";
import { getDeployRecord, type DeployRecordView } from "@/lib/deploymentsApi";
import "@/styles/deploy-follow.css";
import { deployHeadline, deployStanding, deployTone } from "./deployRecordWords";

// 2026-11-18-3863f: where a deploy stands after its run ended — its pull request's merge and CI, or its pipeline run,
// as the server follows them; a red one with each failed check, its excerpt and link. Shown on a deploy run's page and
// under the deploy's line on its ticket. A run that delivered nothing shows nothing.

export function DeployFollowBlock({ runId }: { runId: string }) {
  const [record, setRecord] = useState<DeployRecordView | null>(null);
  useEffect(() => {
    const abort = new AbortController();
    getDeployRecord(runId, abort.signal).then(setRecord).catch(() => setRecord(null));
    return () => abort.abort();
  }, [runId]);
  if (!record) return null;
  return (
    <div className={`deploy-follow ${deployTone(record)}`} data-testid="deploy-follow">
      <div className="deploy-follow-head">
        <a href={record.url} target="_blank" rel="noreferrer" data-testid="deploy-follow-link">{deployHeadline(record)} ↗</a>
        {" · "}
        <span data-testid="deploy-follow-standing">{deployStanding(record)}</span>
      </div>
      {record.note && <div className="msub" data-testid="deploy-follow-note">{record.note}</div>}
      {record.failures.map((f) => (
        <div key={`${f.name}-${f.url ?? ""}`} className="deploy-follow-failure" data-testid="deploy-follow-failure">
          {f.url ? <a href={f.url} target="_blank" rel="noreferrer">{f.name} ↗</a> : <span>{f.name}</span>}
          {f.excerpt && <pre className="mono">{f.excerpt}</pre>}
        </div>
      ))}
    </div>
  );
}
