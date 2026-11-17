"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import type { HubConnectionState } from "@microsoft/signalr";
import { BuildNotes } from "@/components/jobs/story/BuildNotes";
import { LedgerPanel } from "@/components/jobs/story/LedgerPanel";
import { OutcomePanel } from "@/components/jobs/story/OutcomePanel";
import { runLedger } from "@/components/jobs/story/runLedger";
import { PlanPanel, SpecPanel } from "@/components/jobs/story/SpecPlanPanels";
import { SpecsPanel } from "@/components/jobs/story/SpecsPanel";
import { TicketPanel } from "@/components/jobs/story/TicketPanel";
import { VerifySummary } from "@/components/jobs/story/VerifySummary";
import type { RunJudgements } from "@/components/jobs/story/useRunJudgements";
import { buildVerifyFallback } from "@/components/jobs/story/verifyFallback";
import type { RunStepRow } from "@/lib/runStepsApi";
import { runTracePath } from "@/lib/runPath";
import type { RunEvent, RunSnapshot } from "@/types/hub-events";
import "@/styles/run-log.css";
import { RunFacts } from "./RunFacts";
import { RunPageHead } from "./RunPageHead";
import { RunProgressStrip, RunSectionLog } from "./RunSectionLog";
import { checkOf, runSections, type RunSection, type SectionId } from "./runSections";
import { RunStatusBox } from "./RunStatusBox";
import { outcomeSection, planSection, ticketSection } from "./storySections";
import { useRunTicket } from "./useRunTicket";

// 2026-11-16-6877b: the run page's default view — header, one status sentence, the progress strip and the sectioned
// log, every section closed. The deep views stay one click away: every step and the raw trace in the drawer, the
// Why page, and the story graph.
// 2026-11-17-d6fbb: and it is complete without the graph — the side rail's facts sit under the meta line, and the
// story's ticket, spec and plan, and outcome are sections of the log, closed like the others.

interface Props {
  runId: string;
  snapshot: RunSnapshot | null;
  events: RunEvent[];
  steps: RunStepRow[];
  repoNames: string[];
  connectionState: HubConnectionState;
  /** The page's one read of the run's judgements, shared with the graph view. */
  judged: RunJudgements;
  graph: boolean;
  onGraph: () => void;
  onOpenTrace: () => void;
  onDeleted: () => void;
}

export function RunLogView({ runId, snapshot, events, steps, repoNames, connectionState, judged, graph, onGraph, onOpenTrace, onDeleted }: Props) {
  const ticket = useRunTicket(runId, snapshot);
  const fallback = useMemo(() => buildVerifyFallback(events), [events]);
  const ledger = useMemo(() => runLedger(snapshot, events), [snapshot, events]);
  const check = checkOf(snapshot?.acceptance);
  const stepSections = useMemo(() => runSections(steps, events, check.state, check.summary), [steps, events, check.state, check.summary]);
  const sections = useMemo(() => logSections(stepSections, snapshot, ledger), [stepSections, snapshot, ledger]);
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
      <RunFacts snapshot={snapshot} />
      <RunStatusBox snapshot={snapshot} />
      <RunProgressStrip sections={stepSections} open={open} onToggle={toggle} />
      <RunSectionLog sections={sections} open={open} onToggle={toggle} slots={{
        ticket: <TicketPanel snapshot={snapshot} events={events} />,
        plan: (
          <>
            <SpecPanel runId={runId} />
            <PlanPanel runId={runId} />
            {ledger && ledger.length > 0 && <LedgerPanel entries={ledger} />}
            <SpecsPanel runId={runId} revision={events.length} />
            <BuildNotes runId={runId} events={events} />
          </>
        ),
        check: (
          <VerifySummary acceptance={snapshot?.acceptance ?? null} fallback={fallback} judgements={judged.judgements}
            judgementBusy={judged.busy} onRecordJudgement={judged.record} onWithdrawJudgement={judged.withdraw} />
        ),
        outcome: <OutcomePanel runId={runId} snapshot={snapshot} />,
      }} />
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

/** Ticket first, the spec and plan after the spec was derived, the outcome last; the step sections in between. */
function logSections(steps: RunSection[], snapshot: RunSnapshot | null, ledger: ReturnType<typeof runLedger>): RunSection[] {
  const afterSpec = steps.findIndex((s) => s.id === "spec") + 1;
  return [ticketSection(snapshot), ...steps.slice(0, afterSpec), planSection(ledger), ...steps.slice(afterSpec), outcomeSection(snapshot)];
}
