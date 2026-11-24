"use client";

import { useT } from "@/hooks/useT";
import type { DeployRecordView } from "@/lib/deploymentsApi";
import "@/styles/deploy-follow.css";
import { deployHeadline, deployStanding, deployTone } from "./deployRecordWords";

// 2026-11-23-8df6a: one deploy record as a card — what went where with its link, where it stands, why following stopped,
// and a red one's failed checks. The run's and the ticket's follow block and the Deployments page show the same card.

export function DeployRecordCard({ record }: { record: DeployRecordView }) {
  const t = useT();
  return (
    <div className={`deploy-follow ${deployTone(record)}`} data-testid="deploy-follow">
      <div className="deploy-follow-head">
        <a href={record.url} target="_blank" rel="noreferrer" data-testid="deploy-follow-link">{deployHeadline(t, record)} ↗</a>
        {" · "}
        <span data-testid="deploy-follow-standing">{deployStanding(t, record)}</span>
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
