"use client";

import { useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { runLedger } from "@/components/jobs/story/runLedger";
import { useRunSpecs } from "@/hooks/useRunSpecs";
import { isRunLive } from "@/lib/runLiveness";
import type { RunStepRow } from "@/lib/runStepsApi";
import type { RunEvent, RunSnapshot } from "@/types/hub-events";
import "@/styles/run-log.css";
import { RunLog } from "./RunLog";
import { DeliveredBody, PlanBody, SpecBody } from "./RunLogSectionBodies";
import { initiallyOpen, runLogSections, type LogSection } from "./runLogSections";
import { RunStepDetail } from "./RunStepDetail";
import { ledgerSpec } from "./specStepLines";
import type { GivenPasses } from "./useSpecStepLines";

// 2026-11-18-d6d2e: the run page's sectioned log on its own (2026-11-17-6d9eb..6d9ec) — Ticket, Before the work, The plan,
// a section per spec, Checked against the spec, Delivered; a running or stopped section starts open; a step opens its
// detail in place. The run page and the ticket page's report both draw it. The caller owns where the open step lives
// (the run page's address, the report's own state), the ids' prefix, and the two bodies that differ between them:
// the ticket (absent: no Ticket section) and the check.

interface Props {
  runId: string;
  snapshot: RunSnapshot | null;
  events: RunEvent[];
  steps: RunStepRow[];
  /** The open step, by step index. */
  openStep: number | null;
  onOpenStep: (step: number | null) => void;
  /** Prefixed to every element id, so several logs on one page never share one. */
  idPrefix?: string;
  /** The Ticket section's body; without one the log has no Ticket section. */
  ticketBody?: ReactNode;
  checkBody: ReactNode;
  /** The run's passes as the page already read them; an opened spec step reads them itself without. */
  passes?: GivenPasses;
}

function flip(set: ReadonlySet<string>, id: string): Set<string> {
  const next = new Set(set);
  if (!next.delete(id)) next.add(id);
  return next;
}

export function RunSectionedLog({ runId, snapshot, events, steps, openStep, onOpenStep, idPrefix = "", ticketBody, checkBody, passes }: Props) {
  const specs = useRunSpecs(runId, events.length);
  const ledger = useMemo(() => runLedger(snapshot, events), [snapshot, events]);
  const sections = useMemo(() => {
    const all = runLogSections(steps, specs, snapshot, ledger);
    return ticketBody === undefined ? all.filter((s) => s.kind !== "ticket") : all;
  }, [steps, specs, snapshot, ledger, ticketBody]);
  const [toggled, setToggled] = useState<ReadonlySet<string>>(new Set());
  const [mechanics, setMechanics] = useState<ReadonlySet<string>>(new Set());
  const initial = initiallyOpen(sections);
  // 2026-11-18-9fae: only the step named on arrival (the address, read after mount) opens its section; a step clicked
  // later sits in a section the person already opened, and adding it to `initial` would cancel their toggle and close it.
  const stepClicked = useRef(false);
  const arrivalSection = useRef<string | null>(null);
  if (!stepClicked.current && arrivalSection.current === null && openStep !== null)
    arrivalSection.current = sections.find((s) => s.steps.some((st) => st.row.stepIndex === openStep))?.id ?? null;
  if (arrivalSection.current) initial.add(arrivalSection.current);
  // 2026-11-17-6d9ec: a step named on arrival is scrolled to once; a click never scrolls.
  const scrolled = useRef(false);
  useEffect(() => {
    if (scrolled.current || openStep === null) return;
    const line = document.getElementById(`${idPrefix}step-${openStep}`);
    if (!line) return;
    scrolled.current = true;
    line.scrollIntoView?.({ block: "center" });
  }, [openStep, sections, idPrefix]);
  const open = new Set(sections.filter((s) => initial.has(s.id) !== toggled.has(s.id)).map((s) => s.id));
  const hasSpecs = sections.some((s) => s.kind === "spec");
  const ledgerSpecId = ledgerSpec(specs);

  const body = (s: LogSection) => {
    switch (s.kind) {
      case "ticket": return ticketBody;
      case "plan": return <PlanBody runId={runId} ledger={ledger} hasSpecs={hasSpecs} events={events} />;
      case "spec": return <SpecBody runId={runId} spec={specs.find((x) => x.specId === s.specId)} />;
      case "check": return checkBody;
      case "deliver": return <DeliveredBody runId={runId} snapshot={snapshot} />;
      default: return null;
    }
  };

  return (
    <RunLog sections={sections} events={events} open={open} onToggle={(id) => setToggled((t) => flip(t, id))} idPrefix={idPrefix}
      openStep={openStep} onToggleStep={(i) => { scrolled.current = true; stepClicked.current = true; onOpenStep(openStep === i ? null : i); }}
      mechanicsOpen={mechanics} onToggleMechanics={(id) => setMechanics((m) => flip(m, id))}
      body={body} detail={(step) => <RunStepDetail runId={runId} step={step} live={isRunLive(snapshot?.status)} ledger={ledger} ledgerSpecId={ledgerSpecId} passes={passes} />} />
  );
}
