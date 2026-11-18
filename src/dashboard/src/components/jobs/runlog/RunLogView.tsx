"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import Link from "next/link";
import type { HubConnectionState } from "@microsoft/signalr";
import { runLedger } from "@/components/jobs/story/runLedger";
import type { RunJudgements } from "@/components/jobs/story/useRunJudgements";
import { useRunSpecs } from "@/hooks/useRunSpecs";
import type { RunStepRow } from "@/lib/runStepsApi";
import { runTracePath } from "@/lib/runPath";
import type { RunEvent, RunSnapshot } from "@/types/hub-events";
import "@/styles/run-log.css";
import { RunLog } from "./RunLog";
import { CheckBody, DeliveredBody, PlanBody, SpecBody, TicketBody } from "./RunLogSectionBodies";
import { initiallyOpen, runLogSections, type LogSection } from "./runLogSections";
import { RunNow } from "./RunNow";
import { RunPageHead } from "./RunPageHead";
import { RunStepDetail } from "./RunStepDetail";
import { useRunTicket } from "./useRunTicket";
import { useStepAddress } from "./useStepAddress";
import { isRunLive } from "@/lib/runLiveness";
import { DeployFollowBlock } from "@/components/tickets/deploying/DeployFollowBlock";

// 2026-11-16-6877b: the run page's default view. 2026-11-17-6d9ea: the head and the Now block.
// 2026-11-17-6d9eb: then ONE sectioned log, as the approved mock draws it — Ticket, Got ready, The plan, a section per
// spec, Checked against the spec, Delivered — each section and step line with its llm calls, cost and time, and what the
// story view's panels showed drawn plain inside the section it belongs to. A running or stopped section starts open.
// 2026-11-17-6d9ec: a step opens its whole detail in place, and the open step is the page's address.

interface Props {
  runId: string;
  snapshot: RunSnapshot | null;
  events: RunEvent[];
  steps: RunStepRow[];
  repoNames: string[];
  connectionState: HubConnectionState;
  /** The page's one read of the run's judgements. */
  judged: RunJudgements;
  onDeleted: () => void;
}

function flip(set: ReadonlySet<string>, id: string): Set<string> {
  const next = new Set(set);
  if (!next.delete(id)) next.add(id);
  return next;
}

export function RunLogView({ runId, snapshot, events, steps, repoNames, connectionState, judged, onDeleted }: Props) {
  const ticket = useRunTicket(runId, snapshot);
  const specs = useRunSpecs(runId, events.length);
  const ledger = useMemo(() => runLedger(snapshot, events), [snapshot, events]);
  const sections = useMemo(() => runLogSections(steps, specs, snapshot, ledger), [steps, specs, snapshot, ledger]);
  const [toggled, setToggled] = useState<ReadonlySet<string>>(new Set());
  const [mechanics, setMechanics] = useState<ReadonlySet<string>>(new Set());
  // 2026-11-17-6d9ec: the open step is the address (?step=), and its section opens with it.
  const [openStep, setOpenStep] = useStepAddress();
  const initial = initiallyOpen(sections);
  const addressed = sections.find((s) => s.steps.some((st) => st.row.stepIndex === openStep))?.id;
  if (addressed) initial.add(addressed);
  const scrolled = useRef(false);
  useEffect(() => {
    if (scrolled.current || openStep === null) return;
    const line = document.getElementById(`step-${openStep}`);
    if (!line) return;
    scrolled.current = true;
    line.scrollIntoView?.({ block: "center" });
  }, [openStep, sections]);
  const open = new Set(sections.filter((s) => initial.has(s.id) !== toggled.has(s.id)).map((s) => s.id));
  const stepCount = steps.filter((s) => !s.planned).length;
  const hasSpecs = sections.some((s) => s.kind === "spec");

  const body = (s: LogSection) => {
    switch (s.kind) {
      case "ticket": return <TicketBody snapshot={snapshot} events={events} ticket={ticket} />;
      case "plan": return <PlanBody runId={runId} ledger={ledger} hasSpecs={hasSpecs} events={events} />;
      case "spec": return <SpecBody runId={runId} spec={specs.find((x) => x.specId === s.specId)} />;
      case "check": return <CheckBody snapshot={snapshot} events={events} judged={judged} />;
      case "deliver": return <DeliveredBody runId={runId} snapshot={snapshot} />;
      default: return null;
    }
  };

  return (
    <div className="rl-page" data-testid="run-log">
      <RunPageHead runId={runId} snapshot={snapshot} ticket={ticket} repoNames={repoNames} stepCount={stepCount}
        connectionState={connectionState} onDeleted={onDeleted} />
      <RunNow runId={runId} snapshot={snapshot} events={events} steps={steps} ticket={ticket} />
      {snapshot?.pipeline === "deploy" && !isRunLive(snapshot.status) && <DeployFollowBlock runId={runId} />}
      <RunLog sections={sections} events={events} open={open} onToggle={(id) => setToggled((t) => flip(t, id))}
        openStep={openStep} onToggleStep={(i) => { scrolled.current = true; setOpenStep(openStep === i ? null : i); }}
        mechanicsOpen={mechanics} onToggleMechanics={(id) => setMechanics((m) => flip(m, id))}
        body={body} detail={(step) => <RunStepDetail runId={runId} step={step} live={isRunLive(snapshot?.status)} />} />
      <div className="rl-more">
        <Link className="rl-link" href={runTracePath(runId)} data-testid="run-open-why">Why it went this way</Link>
      </div>
    </div>
  );
}
