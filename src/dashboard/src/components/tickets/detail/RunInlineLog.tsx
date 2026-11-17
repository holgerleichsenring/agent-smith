"use client";

import { useMemo, useState } from "react";
import { RunSectionLog } from "@/components/jobs/runlog/RunSectionLog";
import { checkOf, runSections, type SectionId } from "@/components/jobs/runlog/runSections";
import { useRunSteps } from "@/hooks/useRunSteps";
import { useAttemptRun } from "@/lib/tickets/TicketPageStore";
import type { TicketAttempt } from "@/types/tickets";

// 2026-11-17-8d1cb: a run's sectioned log inside its What happened line — the run page's seven sections
// (components/jobs/runlog) over the same step rows, read when the line opens and again when the attempt's status moves.
// A section that stopped or holds a finding opens by itself; every verify pass is its own line in its section. The
// check section lists the run's acceptance verdicts in words; overruling one stays on the run page.

const MARKS: Record<string, string> = { met: "✓", unmet: "✗", unproven: "?", not_applicable: "–" };

export function RunInlineLog({ attempt }: { attempt: TicketAttempt }) {
  const steps = useRunSteps(attempt.runId, attempt.status);
  const acceptance = useAttemptRun(attempt.runId).data?.acceptance ?? null;
  const check = checkOf(acceptance);
  const sections = useMemo(() => runSections(steps, [], check.state, check.summary), [steps, check.state, check.summary]);
  const [toggled, setToggled] = useState<ReadonlySet<SectionId>>(new Set());
  if (steps.length === 0) return null;
  const open = new Set(sections.filter((s) => (s.state === "bad" || s.state === "warn") !== toggled.has(s.id)).map((s) => s.id));
  const toggle = (id: SectionId) => setToggled((now) => {
    const next = new Set(now);
    if (!next.delete(id)) next.add(id);
    return next;
  });
  return (
    <div className="tk-runlog" data-testid={`thread-runlog-${attempt.number}`}>
      <RunSectionLog sections={sections} open={open} onToggle={toggle} check={
        (acceptance?.criteria ?? []).length === 0
          ? <div className="rl-line rl-dim">No verdict recorded.</div>
          : acceptance!.criteria.map((c) => (
            <div key={c.text} className={`rl-line ${c.status === "met" ? "done" : c.status === "unmet" ? "bad" : "warn"}`}>
              <span className="rl-at">{MARKS[c.status] ?? "·"}</span>
              <span>{c.text}{c.reason && <span className="rl-dim">: {c.reason}</span>}</span>
            </div>
          ))
      } />
    </div>
  );
}
