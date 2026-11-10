"use client";

import { useEffect, useState } from "react";
import { fetchDialogActivity } from "@/lib/specDialogApi";
import type { SpecDialogActivityPage, SpecDialogActivityStep, SpecDialogSpend } from "@/types/spec-dialog";
import { durationOf, usd } from "./turnProposal";

// 2026-11-10-d854b: a turn's work in one closed line — how long it worked, how many steps, sub-agent calls, review
// rounds and what it cost. Opened, it lists the turn's steps from its run, a page at a time (d854c), so a turn of
// 1,225 steps is all one click away after a reload without one response carrying all of them.

const PAGE = 200;

export function TurnActivityLine({
  runId,
  rounds,
  spend,
}: {
  runId?: string | null;
  rounds: number;
  spend?: SpecDialogSpend | null;
}) {
  const [first, setFirst] = useState<SpecDialogActivityPage | null>(null);
  const [steps, setSteps] = useState<SpecDialogActivityStep[]>([]);
  const [next, setNext] = useState<{ seq: number | null; more: boolean }>({ seq: null, more: false });
  const [open, setOpen] = useState(false);

  useEffect(() => {
    if (!runId) return;
    const abort = new AbortController();
    fetchDialogActivity(runId, null, PAGE, abort.signal).then((page) => {
      setFirst(page);
      setSteps(page.steps);
      setNext({ seq: page.nextSeq, more: page.hasMore });
    }, () => undefined);
    return () => abort.abort();
  }, [runId]);

  const more = async () => {
    if (!runId) return;
    const page = await fetchDialogActivity(runId, next.seq, PAGE);
    setSteps((held) => [...held, ...page.steps]);
    setNext({ seq: page.nextSeq, more: page.hasMore });
  };

  // Nothing to say: no run to page, nothing spent, no review — a proposal stored before any of it was kept.
  if (!runId && !spend && rounds === 0) return null;
  return (
    <div data-testid="turn-activity">
      <button type="button" className="d-turn-activity" aria-expanded={open} disabled={!runId}
        data-testid="turn-activity-line" onClick={() => setOpen((was) => !was)}>
        <span aria-hidden="true" className="font-mono">{open ? "▾" : "▸"}</span>
        <span>{lineOf(first, rounds)}</span>
        {spend && <span className="cost" data-testid="turn-activity-cost">{usd(spend.usd)}</span>}
      </button>
      {open && (
        <div className="d-turn-steps" data-testid="turn-activity-steps">
          {steps.map((step) => (
            <div key={step.seq + step.timestamp} data-testid="turn-activity-step">
              <span className="font-mono">{step.seq}</span> {step.kind}{step.name ? ` ${step.name}` : ""}
              {step.detail ? ` — ${step.detail}` : ""}
            </div>
          ))}
          {next.more && (
            <button type="button" className="d-link" data-testid="turn-activity-more" onClick={() => void more()}>
              Show the next {PAGE} of {first?.count ?? steps.length}
            </button>
          )}
        </div>
      )}
    </div>
  );
}

function lineOf(page: SpecDialogActivityPage | null, rounds: number): string {
  const worked = durationOf(page?.firstAt, page?.lastAt);
  return [
    worked ? `Worked ${worked}` : "Worked",
    page ? `${page.count.toLocaleString("en-US")} steps` : null,
    page?.subAgentCalls ? `${page.subAgentCalls} sub-agent ${page.subAgentCalls === 1 ? "call" : "calls"}` : null,
    rounds > 0 ? `${rounds} review ${rounds === 1 ? "round" : "rounds"}` : null,
  ].filter(Boolean).join(" · ");
}
