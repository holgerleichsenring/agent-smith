"use client";

import Link from "next/link";
import type { HubConnectionState } from "@microsoft/signalr";
import type { RunJudgements } from "@/components/jobs/story/useRunJudgements";
import { DeployFollowBlock } from "@/components/tickets/deploying/DeployFollowBlock";
import { RUNS_CONTROL } from "@/components/tickets/detail/RetryUnchanged";
import { useLacksPermission } from "@/hooks/useLacksPermission";
import { useRunSpecs } from "@/hooks/useRunSpecs";
import { isRunLive } from "@/lib/runLiveness";
import type { RunStepRow } from "@/lib/runStepsApi";
import { runTracePath } from "@/lib/runPath";
import type { RunEvent, RunSnapshot } from "@/types/hub-events";
import { CheckBody, TicketBody } from "./RunLogSectionBodies";
import { RunNow } from "./RunNow";
import { RunPageHead } from "./RunPageHead";
import { RunSectionedLog } from "./RunSectionedLog";
import { snapshotStatus } from "./runStatusLine";
import { modelWaitHead } from "./modelWaitWords";
import { currentModelWait } from "./runNowLines";
import { useDateLocale, useT } from "@/hooks/useT";
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
  // 2026-11-22-7103a: the run's one status line — the head's state word and How it ended's first line.
  const specs = useRunSpecs(runId, events.length);
  const canRetry = !useLacksPermission(RUNS_CONTROL, ticket?.project ?? snapshot?.project ?? undefined);
  const t = useT();
  const locale = useDateLocale();
  const wait = modelWaitHead(t, currentModelWait(events), locale); // 2026-11-22-50d7a: the head says the run waits for the model
  const status = snapshot ? snapshotStatus(t, snapshot, specs, steps, ticket, canRetry, wait) : null;

  return (
    <div className="rl-page" data-testid="run-log">
      <RunPageHead runId={runId} snapshot={snapshot} line={status} ticket={ticket} repoNames={repoNames} stepCount={stepCount}
        connectionState={connectionState} onDeleted={onDeleted} />
      <RunNow runId={runId} snapshot={snapshot} status={status} events={events} steps={steps} ticket={ticket} />
      {snapshot?.pipeline === "deploy" && !isRunLive(snapshot.status) && <DeployFollowBlock runId={runId} />}
      <RunSectionedLog runId={runId} snapshot={snapshot} events={events} steps={steps} openStep={openStep} onOpenStep={setOpenStep}
        ticketBody={<TicketBody snapshot={snapshot} events={events} ticket={ticket} />}
        checkBody={<CheckBody snapshot={snapshot} events={events} judged={judged} />} />
      <div className="rl-more">
        <Link className="rl-link" href={runTracePath(runId)} data-testid="run-open-why">{t("runs.log.whyLink")}</Link>
      </div>
    </div>
  );
}
