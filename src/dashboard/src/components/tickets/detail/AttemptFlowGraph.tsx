"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import { isRunLive } from "@/lib/runLiveness";
import { useAttemptPasses, useTicketConversation, useTicketSeries } from "@/lib/tickets/TicketPageStore";
import type { TicketAttempt, TicketDetail } from "@/types/tickets";
import { causeWords, money } from "../ticketFormat";
import { layoutAttemptFlow, type AttemptFlowLayout, type FlowMode } from "./attemptFlowLayout";
import { FlowCanvas } from "./FlowCanvas";
import { clock, elapsed, statusWords } from "./flowWords";
import { NodeDetail } from "./NodeDetail";

// 2026-10-13-9a59q: the mock's .graph — attempt switcher, sub-line, passes unrolled or folded, the
// canvas and the legend — then the chosen node's detail below the panel. Every attempt draws from its
// own reads: series?runId=, its passes, its conversation entries.

interface Props {
  detail: TicketDetail;
  attempt: TicketAttempt;
  onSelect: (runId: string) => void;
}

export function AttemptFlowGraph({ detail, attempt, onSelect }: Props) {
  const [mode, setMode] = useState<FlowMode>("unrolled");
  const series = useTicketSeries(attempt.runId);
  const passes = useAttemptPasses(attempt.runId);
  const conversation = useTicketConversation();
  const nextAttempt = detail.attempts.find((a) => a.number === attempt.number + 1) ?? null;
  const entries = useMemo(
    () => conversation.data?.entries.filter((e) => e.attemptRunId === attempt.runId) ?? [],
    [conversation.data, attempt.runId],
  );
  const loading = series.loading || passes.loading;
  const layout = useMemo(
    () => (loading ? null : layoutAttemptFlow({ attempt, nextAttempt, series: series.data, passes: passes.data, entries }, mode)),
    [loading, attempt, nextAttempt, series.data, passes.data, entries, mode],
  );
  const [node, setNode] = useState<string | null>(null);
  useEffect(() => setNode(null), [attempt.runId]);
  const shown = node ?? (layout ? openNode(layout) : null);

  return (
    <>
      <div className="graph" data-testid="ticket-graph">
        <div className="g-top" role="tablist" aria-label="Attempts">
          {detail.attempts.map((a) => (
            <button key={a.runId} type="button" className="gt" role="tab" aria-selected={a.runId === attempt.runId}
              onClick={() => onSelect(a.runId)} data-testid={`attempt-switch-${a.number}`}
              title={a.endedOnInfrastructure ? "ended on infrastructure" : undefined}>
              <span className={`gd ${dotClass(a)}`} />Attempt {a.number}
            </button>
          ))}
        </div>
        <div className="g-sub">
          <span data-testid="attempt-sub"><SubLine attempt={attempt} /></span>
          <span className="modes" aria-label="Build and verify passes">
            {(["unrolled", "folded"] as const).map((m) => (
              <button key={m} type="button" className="lm" aria-pressed={mode === m} onClick={() => setMode(m)}>
                {m === "unrolled" ? "passes unrolled" : "folded"}
              </button>
            ))}
          </span>
        </div>
        <GraphBody layout={layout} attempt={attempt} selected={shown} onSelect={setNode} />
        <Legend />
      </div>
      {layout && !layout.fallback && shown && (
        <NodeDetail
          layout={layout} nodeId={shown} detail={detail} attempt={attempt} nextAttempt={nextAttempt}
          series={series.data} passes={passes.data} entries={entries}
        />
      )}
    </>
  );
}

function GraphBody({ layout, attempt, selected, onSelect }: {
  layout: AttemptFlowLayout | null; attempt: TicketAttempt; selected: string | null; onSelect: (id: string) => void;
}) {
  if (!layout) return <div className="g-note" data-testid="attempt-flow-loading">Reading attempt {attempt.number}…</div>;
  if (layout.fallback) {
    return (
      <div className="g-note" data-testid="attempt-flow-fallback">
        Attempt {attempt.number} ran before attempts recorded their cut, so it has no graph.{" "}
        <Link href={`/jobs/${encodeURIComponent(attempt.runId)}`}>Open its run viewer</Link>
      </div>
    );
  }
  return <FlowCanvas layout={layout} selected={selected} onSelect={onSelect} />;
}

function SubLine({ attempt }: { attempt: TicketAttempt }) {
  const parts = [clock(attempt.startedAt)];
  return (
    <>
      {parts.filter(Boolean).join("")}
      {attempt.cause && <> · <span className="cause">{causeWords(attempt.cause)}</span></>}
      {" · "}<b>{attempt.handedBack ? "handed back" : statusWords(attempt.status)}</b>
      {` · ${elapsed(attempt.startedAt, attempt.finishedAt)} · ${money(attempt.cost)}`}
    </>
  );
}

function Legend() {
  return (
    <div className="g-legend">
      <span><i className="lg-done" />done here</span>
      <span><i className="lg-carried" />carried, cost nothing</span>
      <span><i className="lg-live" />working now</span>
      <span><i className="lg-bad" />stopped here</span>
      <span><i className="lg-person" />waits for a person</span>
      <span><i className="lg-back" />↺ re-engaged by the master</span>
    </div>
  );
}

/** The switcher's dot: handed back waits on a person; a live attempt works; else how it ended. */
export function dotClass(attempt: TicketAttempt): string {
  if (attempt.handedBack) return "hb";
  if (isRunLive(attempt.status)) return "run";
  if (attempt.endedOnInfrastructure) return "infra";
  return attempt.status === "success" || attempt.status === "shortfall" ? "ok" : "bad";
}

/** The node a fresh attempt opens on: what works now, else the tail. */
function openNode(layout: AttemptFlowLayout): string | null {
  const live = layout.nodes.filter((n) => n.state === "live");
  if (live.length > 0) return live[live.length - 1].id;
  return layout.nodes.find((n) => n.kind === "tail")?.id ?? layout.nodes[layout.nodes.length - 1]?.id ?? null;
}
