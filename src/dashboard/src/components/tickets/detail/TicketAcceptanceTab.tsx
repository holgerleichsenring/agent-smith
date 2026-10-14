"use client";

import { useTicketSeries } from "@/lib/tickets/TicketPageStore";
import { acceptanceTally, criterionRow, overruleWords } from "./specWords";

// 2026-10-13-9a59r: acceptance PER SPEC — each spec's done list (a run's acceptance contract is the current
// spec's), every criterion with its mark and the attempt that verified it, "verify pending" or "not started";
// an operator judgement shows as the overruled disposition beside the machine's. Overruling stays in the
// run viewer: nothing here records one.

export function TicketAcceptanceTab() {
  const series = useTicketSeries();
  const tally = acceptanceTally(series.data);
  const specs = [...(series.data?.specs ?? [])].sort((l, r) => l.ordinal - r.ordinal);
  return (
    <>
      <div className="section-head first">
        <h2>Acceptance</h2>
        {tally && <span className="cnt" data-testid="acceptance-tally">{tally.verified} of {tally.total} verified</span>}
        <span className="sh-sub">each spec&apos;s done criteria · the attempt that verified them</span>
      </div>
      {specs.length === 0 && <div className="msub">{series.loading ? "Reading the series…" : "No spec has been cut for this ticket."}</div>}
      <div className="acc-list">
        {specs.map((spec) => {
          const standing = spec.state === "in_progress" ? " · working" : spec.state === "not_started" ? " · not started" : "";
          return (
            <div key={spec.specId} className="doc" data-testid={`acceptance-spec-${spec.specId}`}>
              <h5>Spec {spec.ordinal}{spec.repos.length > 0 ? ` · ${spec.repos.join(", ")}` : ""}{standing}</h5>
              <div className="crit">
                {spec.done.map((criterion) => {
                  const row = criterionRow(criterion, spec);
                  const overruled = overruleWords(criterion);
                  return (
                    <div key={criterion.text} className="ci" data-testid="acceptance-criterion">
                      <span className={`m ${row.mark}`}>{row.glyph}</span>
                      <span>
                        {criterion.text}
                        {overruled && <span className="dim-line" data-testid="acceptance-overruled"> · {overruled}</span>}
                      </span>
                      <span className="cw">{row.who}</span>
                    </div>
                  );
                })}
              </div>
            </div>
          );
        })}
      </div>
    </>
  );
}
