"use client";

import Link from "next/link";
import type { HubConnectionState } from "@microsoft/signalr";
import type { RunJudgements } from "@/components/jobs/story/useRunJudgements";
import { DeployFollowBlock } from "@/components/tickets/deploying/DeployFollowBlock";
import { isRunLive } from "@/lib/runLiveness";
import type { RunStepRow } from "@/lib/runStepsApi";
import { runTracePath } from "@/lib/runPath";
import type { RunEvent, RunSnapshot } from "@/types/hub-events";
import { CheckBody, TicketBody } from "./RunLogSectionBodies";
import { RunNow } from "./RunNow";
import { RunPageHead } from "./RunPageHead";
import { RunSectionedLog } from "./RunSectionedLog";
import { useRunTicket } from "./useRunTicket";
import { useStepAddress } from "./useStepAddress";

// 2026-11-16-6877b: the run page's default view. 2026-11-17-6d9ea: the head and the Now block.
// 2026-11-17-6d9eb: then ONE sectioned log, as the approved mock draws it — Ticket, Before the work, The plan, a section per
// spec, Checked against the spec, Delivered — each section and step line with its llm calls, cost and time, and what the
// story view's panels showed drawn plain inside the section it belongs to. A running or stopped section starts open.
// 2026-11-17-6d9ec: a step opens its whole detail in place, and the open step is the page's address.
// 2026-11-18-d6d2e: the log itself is RunSectionedLog, which the ticket page draws too; here it carries the ticket as the
// run read it and the criteria with their overrule.

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

export function RunLogView({ runId, snapshot, events, steps, repoNames, connectionState, judged, onDeleted }: Props) {
  const ticket = useRunTicket(runId, snapshot);
  const [openStep, setOpenStep] = useStepAddress();
  const stepCount = steps.filter((s) => !s.planned).length;

  return (
    <div className="rl-page" data-testid="run-log">
      <RunPageHead runId={runId} snapshot={snapshot} ticket={ticket} repoNames={repoNames} stepCount={stepCount}
        connectionState={connectionState} onDeleted={onDeleted} />
      <RunNow runId={runId} snapshot={snapshot} events={events} steps={steps} ticket={ticket} />
      {snapshot?.pipeline === "deploy" && !isRunLive(snapshot.status) && <DeployFollowBlock runId={runId} />}
      <RunSectionedLog runId={runId} snapshot={snapshot} events={events} steps={steps} openStep={openStep} onOpenStep={setOpenStep}
        ticketBody={<TicketBody snapshot={snapshot} events={events} ticket={ticket} />}
        checkBody={<CheckBody snapshot={snapshot} events={events} judged={judged} />} />
      <div className="rl-more">
        <Link className="rl-link" href={runTracePath(runId)} data-testid="run-open-why">Why it went this way</Link>
      </div>
    </div>
  );
}
