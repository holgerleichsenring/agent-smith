"use client";

import Link from "next/link";
import { runPath } from "@/lib/runPath";

import { useAttemptReads, useTicketDetail, useTicketSeries } from "@/lib/tickets/TicketPageStore";
import type { TicketAttempt, TicketSeriesSpec } from "@/types/tickets";
import { clock } from "./flowWords";
import { SeriesContractReview } from "./SeriesContractReview";
import { SpecDocument } from "./SpecDocument";
import { attemptCell, defaultSpec, isRunRecord, RUN_RECORD_LINE, type AttemptCell } from "./specWords";
import { SPEC_PARAM, useTicketTabGo } from "./useTicketTabGo";

// 2026-10-13-9a59r: the series as read-only documents — one list item per spec with a cell per attempt
// (from that attempt's own cut and passes, read once per finished attempt through the page's store) beside
// the selected spec's document; then the revisions the attempts recorded. Specs change only through the
// conversation, so nothing here edits or fetches a spec file. Attempts that predate the cut show the specs
// their runs recorded, and say so. 2026-10-16-86e6d: the cell row sits beside the item button (a link cannot sit in a
// button), and the cell of an attempt that ran the spec opens its run.

export function TicketSpecsTab() {
  const detail = useTicketDetail();
  const series = useTicketSeries();
  const address = useTicketTabGo();
  const attempts = detail.data?.attempts ?? [];
  const reads = useAttemptReads(attempts.map((a) => a.runId));
  const specs = [...(series.data?.specs ?? [])].sort((l, r) => l.ordinal - r.ordinal);
  if (!series.data?.recorded || specs.length === 0) {
    return (
      <>
        <SpecsHead specs={specs} />
        <div className="msub" data-testid="specs-none">{series.loading ? "Reading the series…" : "No spec has been cut for this ticket."}</div>
      </>
    );
  }
  const selected = specs.find((s) => s.specId === address.param(SPEC_PARAM)) ?? defaultSpec(specs);
  const runRecord = isRunRecord(series.data);
  return (
    <>
      <SpecsHead specs={specs} />
      {runRecord && <div className="msub" data-testid="specs-run-record">{RUN_RECORD_LINE}</div>}
      <div className="sp-grid">
        <div className="sp-list" aria-label="Specs of this ticket" data-testid="specs-list">
          {specs.map((spec) => (
            <div key={spec.specId} className="sp-entry">
              <button type="button" className="sp-item" aria-current={spec.specId === selected?.specId}
                onClick={() => address.go("specs", { [SPEC_PARAM]: spec.specId })} data-testid={`spec-item-${spec.specId}`}>
                <span className="si-top"><span className="si-n">{spec.ordinal}</span><span className="si-t">{spec.goal}</span></span>
              </button>
              <span className="si-cells">
                {attempts.map((attempt) => (
                  <SpecCell key={attempt.runId} specId={spec.specId} attempt={attempt}
                    cell={attemptCell(spec.specId, attempt, reads.series[attempt.runId], reads.passes[attempt.runId])} />
                ))}
                <span className="al mono">{spec.repos.join(", ")}</span>
              </span>
            </div>
          ))}
        </div>
        {selected && <SpecDocument spec={selected} attempts={attempts} runRecord={runRecord}
          onDiscuss={() => address.go("conversation")} />}
      </div>
      <SeriesContractReview onSelect={(specId) => address.go("specs", { [SPEC_PARAM]: specId })} />
      <Revisions attempts={attempts} />
    </>
  );
}

const RAN = new Set(["done", "carried", "fail", "live"]);

/** 2026-10-16-86e6d: a cell of an attempt that ran the spec opens that attempt's run; none and not-started stay spans. */
function SpecCell({ specId, attempt, cell }: { specId: string; attempt: TicketAttempt; cell: AttemptCell }) {
  const props = { className: `cell ${cell.kind}`, title: cell.title, "data-testid": `spec-cell-${specId}-${attempt.number}`, "data-kind": cell.kind };
  return RAN.has(cell.kind)
    ? <Link href={runPath(attempt.runId)} {...props}>{cell.text}</Link>
    : <span {...props}>{cell.text}</span>;
}

function SpecsHead({ specs }: { specs: TicketSeriesSpec[] }) {
  const done = specs.filter((s) => s.state === "done" || s.state === "carried").length;
  return (
    <div className="section-head first">
      <h2>Specs</h2>
      {specs.length > 0 && <span className="cnt green" data-testid="specs-done">{done} of {specs.length} done</span>}
      <span className="sh-sub">the series as it is being worked · read-only, changed only through the conversation</span>
    </div>
  );
}

function Revisions({ attempts }: { attempts: TicketAttempt[] }) {
  const seen = new Set<number>();
  const rows = attempts.filter((a) => {
    if (!a.revision || seen.has(a.revision.number)) return false;
    seen.add(a.revision.number);
    return true;
  });
  if (rows.length === 0) return null;
  return (
    <>
      <div className="section-head">
        <h2>Revisions</h2><span className="cnt">{rows.length}</span>
        <span className="sh-sub">why the series changed · executed specs are never cut again</span>
      </div>
      <div className="revs" data-testid="specs-revisions">
        {rows.map((a) => (
          <div key={a.revision!.number} className="rv">
            <span className="rn">rev {a.revision!.number}</span>
            <span className="rc">{a.revision!.cause ?? "no cause recorded"}</span>
            <span className="rw">A{a.number}{a.revision!.approvalAt ? ` · approved ${clock(a.revision!.approvalAt)}` : ""}</span>
          </div>
        ))}
      </div>
    </>
  );
}
