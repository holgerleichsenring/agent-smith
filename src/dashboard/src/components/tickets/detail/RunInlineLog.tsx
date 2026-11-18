"use client";

import Link from "next/link";
import { useState } from "react";
import { RunSectionedLog } from "@/components/jobs/runlog/RunSectionedLog";
import { useRunEvents } from "@/hooks/useRunEvents";
import { useRunSteps } from "@/hooks/useRunSteps";
import { runPath } from "@/lib/runPath";
import { useAttemptRun } from "@/lib/tickets/TicketPageStore";
import type { TicketAttempt } from "@/types/tickets";

// 2026-11-17-8d1cb: a run's sectioned log inside its What happened line, read when the line opens.
// 2026-11-18-d6d2e: the run page's own log (RunSectionedLog) — a section per spec with its steps done of planned, the
// mechanics folded, each step opening its detail with its calls and commands. The run is the page store's read of it,
// followed on the hub's RunsChanged; its events and steps come as on the run page. The open step is this line's own,
// so the ticket page's address never changes. No Ticket section (the thread opens with the ticket); each verdict and
// its overrule stay on the run page, the spec card shows the verdicts.

export function RunInlineLog({ attempt }: { attempt: TicketAttempt }) {
  const snapshot = useAttemptRun(attempt.runId).data ?? null;
  const events = useRunEvents(attempt.runId);
  const steps = useRunSteps(attempt.runId, snapshot ?? attempt.status);
  const [openStep, setOpenStep] = useState<number | null>(null);
  if (steps.length === 0) return null;
  return (
    <div className="tk-runlog" data-testid={`thread-runlog-${attempt.number}`}>
      <RunSectionedLog runId={attempt.runId} snapshot={snapshot} events={events} steps={steps} openStep={openStep}
        onOpenStep={setOpenStep} idPrefix={`r-${attempt.runId}-`}
        checkBody={(
          <div className="rl-body-line rl-dim" data-testid={`thread-runlog-check-${attempt.number}`}>
            <Link href={runPath(attempt.runId)}>Open the run</Link> to read each verdict and overrule one.
          </div>
        )} />
    </div>
  );
}
