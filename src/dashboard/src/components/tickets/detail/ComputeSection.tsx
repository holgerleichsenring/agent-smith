"use client";

import { isRunLive } from "@/lib/runLiveness";
import type { RunCompute, TicketAttempt } from "@/types/tickets";
import { computeSegments, wallScale } from "./computeSegments";
import { duration, podEndWords, podSpans, type AttemptMachine, type ComputeTotals, type PersonWait } from "./computeTotals";

// 2026-10-13-9a59s: the Compute section — four tiles, the waiting banner, and per attempt a wall-clock bar on
// one scale with its launches, parks and capacity waits, the pods line and the sandboxes no row counts.

export function ComputeTiles({ totals }: { totals: ComputeTotals }) {
  const runEnd = Math.round(totals.podMinutesToRunEnd);
  const clippedNote = totals.unobserved > 0 && runEnd !== Math.round(totals.podMinutes)
    ? ` · ${totals.unobserved} unobserved clipped at their segment's end; the run detail charges them to run end: ${runEnd} pod-minutes`
    : "";
  const waits = totals.waits.length;
  return (
    <div className="health four" data-testid="compute-tiles">
      <div className="metric"><span className="k">Pod time</span>
        <span className="v">{duration(totals.podMinutes * 60000)}<small data-testid="compute-pod-note">{Math.round(totals.podMinutes)} pod-minutes{clippedNote}</small></span></div>
      <div className="metric"><span className="k">Memory reserved</span>
        <span className="v" data-testid="compute-reserved">
          {totals.reservedGiHours !== null ? `${totals.reservedGiHours.toFixed(1)} Gi·h` : "—"}
          <small>{totals.reservedGiHours !== null ? "a reservation · finished attempts only" : "no reservation recorded"}</small>
        </span></div>
      <div className="metric"><span className="k">Model call time</span>
        <span className="v" data-testid="compute-model">{duration(totals.modelMs)}<small>summed call durations · they overlap when sub-agents run in parallel</small></span></div>
      <div className={totals.waitingMs > 0 ? "metric hot" : "metric"} data-testid="compute-waiting"><span className="k">Waiting on you</span>
        <span className="v">{duration(totals.waitingMs)}<small>{waits === 1 ? "1 wait" : `${waits} waits`}
          {totals.capacityMs > 0 ? ` · ${duration(totals.capacityMs)} waiting for capacity, not counted` : ""}</small></span></div>
    </div>
  );
}

function waitWords(wait: PersonWait): string {
  const span = `${duration(wait.to - wait.from)}${wait.open ? " so far" : ""}`;
  return wait.kind === "handback"
    ? `the hand-back of attempt ${wait.attempt} waited ${span} for a reply`
    : `a question parked attempt ${wait.attempt} for ${span}`;
}

export function WaitingBanner({ totals, onRead }: { totals: ComputeTotals; onRead: () => void }) {
  if (totals.waits.length === 0 || totals.waitingMs <= totals.modelMs) return null;
  const n = totals.waits.length;
  return (
    <div className="banner need compute-banner" data-testid="compute-banner">
      <span className="bi">!</span>
      <div>
        <div className="bt">{n === 1 ? "Waiting on you" : `${n} waits on you`} took longer than all of this ticket&apos;s model call time.</div>
        <div className="bs">
          {totals.waits.map(waitWords).join("; ")}.{" "}
          {totals.podsHeldWhileWaiting ? "A pod's lifetime overlapped a wait." : "No pods were held while waiting — a park releases its sandboxes."}
        </div>
      </div>
      <button className="btn" type="button" onClick={onRead}>{n === 2 ? "Read both" : "Read in conversation"}</button>
    </div>
  );
}

export function ComputeTable({ machines, now }: { machines: AttemptMachine[]; now: number }) {
  const clocks = machines.map((m) => computeSegments(m.attempt, m.compute, now));
  const width = wallScale(clocks);
  return (
    <div className="compute" data-testid="compute-table">
      <div className="c-head"><span>Att.</span><span>Wall clock · what the pods did in it</span><span className="r">Pods</span><span className="r">Pod-min</span></div>
      {machines.map((machine, i) => (
        <ComputeRow key={machine.attempt.runId} attempt={machine.attempt} compute={machine.compute} now={now}
          clock={clocks[i]} width={width(clocks[i].wallMs)} />
      ))}
      <div className="c-legend">
        <span><i className="seg" />launched · call time written beside</span>
        <span><i className="park" />parked on you · no pods, no tokens</span>
        <span><i className="cap" />waiting for capacity</span>
        <span><i className="over" />not running</span>
      </div>
    </div>
  );
}

interface RowProps {
  attempt: TicketAttempt;
  compute: RunCompute | null;
  now: number;
  clock: ReturnType<typeof computeSegments>;
  width: number;
}

function ComputeRow({ attempt, compute, now, clock, width }: RowProps) {
  const pods = podSpans(attempt, compute, now);
  const live = isRunLive(attempt.status);
  const repos = [...new Set(pods.map((p) => p.repo))];
  const podMinutes = Math.round(pods.reduce((sum, p) => sum + (p.to - p.from), 0) / 60000);
  return (
    <div className="c-row" data-testid={`compute-row-${attempt.number}`}>
      <span className="c-a">A{attempt.number}</span>
      <div className="c-track">
        <div className="c-bar" style={{ width: `${width}%` }} data-testid={`compute-bar-${attempt.number}`}>
          {clock.pieces.map((piece, i) => (
            <span key={i} className={piece.kind === "segment" ? "seg" : piece.kind === "capacity" ? "cap" : piece.kind === "idle" ? "over" : "park"}
              style={{ width: `${piece.pct}%` }} title={piece.title} data-kind={piece.kind} />
          ))}
        </div>
        {clock.figures.length > 0 && (
          <span className="c-figs" data-testid={`compute-figures-${attempt.number}`}>
            {clock.figures.map((f) => `launch ${f.launch} · ${duration(f.wallMs)} wall · ${duration(f.callMs)} call time`).join("  ·  ")}
          </span>
        )}
        <div className="c-pods" data-testid={`compute-pods-${attempt.number}`}>
          {repos.length === 0 && <span className="pd">{compute ? "no sandbox recorded" : "reading the sandboxes…"}</span>}
          {repos.map((repo) => {
            const mine = pods.filter((p) => p.repo === repo);
            const lifetimes = mine.map((p) => duration(p.to - p.from)).join(" + ");
            const ends = [...new Set(mine.map((p) => podEndWords(p.endReason, live)))].join(", ");
            return <span key={repo} className="pd"><b>{repo}</b> · {lifetimes} · {ends}</span>;
          })}
          {compute && compute.uncounted.length > 0 && (
            <span className="pd">not counted: {compute.uncounted.map((u) => u.label).join(", ")}</span>
          )}
        </div>
      </div>
      <span className="c-n">{pods.length > 0 ? pods.length : "—"}</span>
      <span className="c-n">{pods.length > 0 ? podMinutes : "—"}</span>
    </div>
  );
}
