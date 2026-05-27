"use client";

import { useMemo } from "react";
import type { RunEvent, RunSnapshot } from "@/types/hub-events";
import { EventType } from "@/types/hub-events";
import { StepProgressList, type StepRow } from "./StepProgressList";

interface Props {
  runId: string;
  snapshot: RunSnapshot | null;
  events: RunEvent[];
}

export function TopologyCard({ runId, snapshot, events }: Props) {
  const steps = useMemo(() => buildStepRows(events), [events]);
  return (
    <section className="rounded-lg border border-stone-200 bg-white p-5" data-testid="topology-card">
      <header className="mb-4">
        <h2 className="text-lg font-medium text-stone-900">
          {snapshot?.pipeline ?? "run"}{" "}
          <span className="text-sm font-normal text-stone-500">{runId}</span>
        </h2>
        <p className="mt-1 text-xs text-stone-500">
          {snapshot?.repos.join(", ") || "no repos"}
          {snapshot?.totalSteps ? ` · step ${snapshot.stepIndex}/${snapshot.totalSteps}` : ""}
        </p>
      </header>
      <StepProgressList steps={steps} />
    </section>
  );
}

function buildStepRows(events: RunEvent[]): StepRow[] {
  const rows = new Map<number, StepRow>();
  for (const event of events) {
    if (event.type === EventType.StepStarted) {
      const e = event;
      rows.set(e.stepIndex, {
        index: e.stepIndex,
        name: e.stepName,
        status: "running",
        durationMs: null,
      });
    } else if (event.type === EventType.StepFinished) {
      const e = event;
      const existing = rows.get(e.stepIndex);
      if (existing) {
        rows.set(e.stepIndex, { ...existing, status: e.status, durationMs: e.durationMs });
      } else {
        rows.set(e.stepIndex, {
          index: e.stepIndex,
          name: `step ${e.stepIndex}`,
          status: e.status,
          durationMs: e.durationMs,
        });
      }
    }
  }
  return [...rows.values()].sort((a, b) => a.index - b.index);
}
