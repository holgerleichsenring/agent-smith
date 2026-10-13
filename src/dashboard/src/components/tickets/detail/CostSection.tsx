"use client";

import { isRunLive } from "@/lib/runLiveness";
import type { AttemptMachines } from "@/lib/tickets/TicketPageStore";
import type { TicketAttempt } from "@/types/tickets";
import { money } from "../ticketFormat";
import { costBuckets, costParts, costScale, specLabel, type CostBucketKind } from "./costScale";

// 2026-10-13-9a59s: the Cost section — one bar per attempt on one scale, split by 9a59f's passes read, the
// run's own cap marked where it is smaller than the full width, carried specs written at $0.

const SPAN_CLASS: Record<CostBucketKind, string> = { derive: "dv", spec: "", pr: "", other: "", unattributed: "na" };

interface Props {
  attempts: TicketAttempt[];
  machines: AttemptMachines;
  ordinals: Readonly<Record<string, number>>;
}

export function CostSection({ attempts, machines, ordinals }: Props) {
  const total = attempts.reduce((sum, a) => sum + a.cost, 0);
  const scale = costScale(attempts.map((a) => ({ cost: a.cost, cap: machines.run[a.runId]?.data?.budgetCapUsd ?? null })));
  const capWords = scale.capIsFull && scale.largestCap !== null
    ? `the ${money(scale.largestCap)} run cap is the full width`
    : `the largest attempt cost (${money(scale.full)}) is the full width`;

  return (
    <>
      <div className="section-head first">
        <h2>Cost</h2><span className="cnt" data-testid="cost-total">{money(total)}</span>
        <span className="sh-sub">
          per attempt, split by spec and pass · {scale.largestCap !== null ? `cap is per run (largest ${money(scale.largestCap)})` : "no run cap recorded"}
        </span>
      </div>
      <div className="costs" data-testid="cost-rows">
        {attempts.map((attempt, i) => {
          const live = isRunLive(attempt.status);
          const row = scale.rows[i];
          const buckets = costBuckets(machines.passes[attempt.runId]?.data ?? null, ordinals);
          const carried = (attempt.revision?.carriedIds ?? []).map((id) => specLabel(id, ordinals));
          return (
            <div key={attempt.runId} className="cr" data-testid={`cost-row-${attempt.number}`}>
              <span className="cl">A{attempt.number}</span>
              <div className="track">
                <div className="bar-wrap">
                  <div className="bar" data-testid={`cost-bar-${attempt.number}`}>
                    {buckets.length > 0
                      ? buckets.map((b) => (
                        <span key={`${b.kind}-${b.label}`} className={live && b.kind !== "derive" ? "live" : SPAN_CLASS[b.kind]}
                          style={{ width: `${scale.pct(b.usd)}%` }} title={`${b.label} ${money(b.usd)}`} />
                      ))
                      : <span className={live ? "live" : "dv"} style={{ width: `${row.costPct}%` }} />}
                  </div>
                  {row.capPct !== null && (
                    <span className={row.over ? "capmark over" : "capmark"} style={{ left: `${row.capPct}%` }}
                      title={`this run's cap`} data-testid={`cost-cap-${attempt.number}`} />
                  )}
                </div>
                <span className="parts" data-testid={`cost-parts-${attempt.number}`}>
                  {costParts(buckets, carried, live) || (live ? "so far" : "no split recorded")}
                </span>
              </div>
              <span className="cv">{money(attempt.cost)}</span>
            </div>
          );
        })}
        <div className="cr total">
          <span className="cl">all</span>
          <span className="parts sans" data-testid="cost-scale">Bars share one scale: {capWords}.</span>
          <span className="cv">{money(total)}</span>
        </div>
      </div>
    </>
  );
}
