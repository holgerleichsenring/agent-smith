"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import type { HubConnectionState } from "@microsoft/signalr";
import { VerifySummary } from "@/components/jobs/story/VerifySummary";
import { useRunJudgements } from "@/components/jobs/story/useRunJudgements";
import { buildVerifyFallback } from "@/components/jobs/story/verifyFallback";
import type { RunStepRow } from "@/lib/runStepsApi";
import { runTracePath } from "@/lib/runPath";
import type { RunEvent, RunSnapshot } from "@/types/hub-events";
import "@/styles/run-log.css";
import { RunPageHead } from "./RunPageHead";
import { RunProgressStrip, RunSectionLog } from "./RunSectionLog";
import { checkOf, runSections, type SectionId } from "./runSections";
import { RunStatusBox } from "./RunStatusBox";
import { useRunTicket } from "./useRunTicket";

// 2026-11-16-6877b: the run page's default view — header, one status sentence, the progress strip and the sectioned
// log, every section closed. The deep views stay one click away: every step and the raw trace in the drawer, the
// Why page, and the story graph.

interface Props {
  runId: string;
  snapshot: RunSnapshot | null;
  events: RunEvent[];
  steps: RunStepRow[];
  repoNames: string[];
  connectionState: HubConnectionState;
  graph: boolean;
  onGraph: () => void;
  onOpenTrace: () => void;
  onDeleted: () => void;
}

export function RunLogView({ runId, snapshot, events, steps, repoNames, connectionState, graph, onGraph, onOpenTrace, onDeleted }: Props) {
  const ticket = useRunTicket(runId, snapshot);
  const judged = useRunJudgements(runId, Boolean(snapshot?.acceptance));
  const fallback = useMemo(() => buildVerifyFallback(events), [events]);
  const check = checkOf(snapshot?.acceptance);
  const sections = useMemo(() => runSections(steps, events, check.state, check.summary), [steps, events, check.state, check.summary]);
  const [open, setOpen] = useState<ReadonlySet<SectionId>>(new Set());
  const toggle = (id: SectionId) => setOpen((now) => {
    const next = new Set(now);
    if (!next.delete(id)) next.add(id);
    return next;
  });
  const stepCount = steps.filter((s) => !s.planned).length;
  return (
    <div className="rl-page" data-testid="run-log">
      <RunPageHead runId={runId} snapshot={snapshot} ticket={ticket} repoNames={repoNames}
        connectionState={connectionState} onDeleted={onDeleted} />
      <RunStatusBox snapshot={snapshot} />
      <RunProgressStrip sections={sections} open={open} onToggle={toggle} />
      <RunSectionLog sections={sections} open={open} onToggle={toggle} check={
        <VerifySummary acceptance={snapshot?.acceptance ?? null} fallback={fallback} judgements={judged.judgements}
          judgementBusy={judged.busy} onRecordJudgement={judged.record} onWithdrawJudgement={judged.withdraw} />
      } />
      <div className="rl-more">
        <button type="button" className="rl-link" onClick={onOpenTrace} data-testid="run-open-trace">
          Show all {stepCount === 1 ? "1 step" : `${stepCount} steps`} and the raw trace
        </button>
        <Link className="rl-link" href={runTracePath(runId)} data-testid="run-open-why">Why it went this way</Link>
        <button type="button" className="rl-link" aria-pressed={graph} onClick={onGraph} data-testid="run-show-graph">
          {graph ? "Hide the graph" : "Show as graph"}
        </button>
      </div>
    </div>
  );
}
