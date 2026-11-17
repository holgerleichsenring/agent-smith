"use client";

import { duration, type ComputeTotals, type PersonWait } from "./computeTotals";

// 2026-10-13-9a59s: the Compute section — four figures and the waiting line. 2026-11-17-8d1cc: plain key/value lines;
// the per-attempt wall-clock bars are gone (each run's pods are on its run page).

export function ComputeTiles({ totals }: { totals: ComputeTotals }) {
  const runEnd = Math.round(totals.podMinutesToRunEnd);
  const clippedNote = totals.unobserved > 0 && runEnd !== Math.round(totals.podMinutes)
    ? ` · ${totals.unobserved} unobserved clipped at their segment's end; the run detail charges them to run end: ${runEnd} pod-minutes`
    : "";
  const waits = totals.waits.length;
  return (
    <div className="tk-kv" data-testid="compute-tiles">
      <div className="tk-kv-row"><span className="k">pod time</span>
        <span className="v">{duration(totals.podMinutes * 60000)} <span className="tk-dim" data-testid="compute-pod-note">{Math.round(totals.podMinutes)} pod-minutes{clippedNote}</span></span></div>
      <div className="tk-kv-row"><span className="k">memory reserved</span>
        <span className="v" data-testid="compute-reserved">
          {totals.reservedGiHours !== null ? `${totals.reservedGiHours.toFixed(1)} Gi·h` : "—"}{" "}
          <span className="tk-dim">{totals.reservedGiHours !== null ? "a reservation · finished runs only" : "no reservation recorded"}</span>
        </span></div>
      <div className="tk-kv-row"><span className="k">model call time</span>
        <span className="v" data-testid="compute-model">{duration(totals.modelMs)} <span className="tk-dim">summed call durations · they overlap when sub-agents run in parallel</span></span></div>
      <div className={totals.waitingMs > 0 ? "tk-kv-row hot" : "tk-kv-row"} data-testid="compute-waiting"><span className="k">waiting on you</span>
        <span className="v">{duration(totals.waitingMs)} <span className="tk-dim">{waits === 1 ? "1 wait" : `${waits} waits`}
          {totals.capacityMs > 0 ? ` · ${duration(totals.capacityMs)} waiting for capacity, not counted` : ""}</span></span></div>
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
    <div className="tk-wait-line" data-testid="compute-banner">
      <span className="tk-dot" aria-hidden="true" />
      <span>
        {n === 1 ? "Waiting on you" : `${n} waits on you`} took longer than all of this ticket&apos;s model call time:{" "}
        {totals.waits.map(waitWords).join("; ")}.{" "}
        {totals.podsHeldWhileWaiting ? "A pod's lifetime overlapped a wait." : "No pods were held while waiting — a park releases its sandboxes."}{" "}
        <button className="tk-link" type="button" onClick={onRead}>{n === 2 ? "Read both" : "Read in What happened"}</button>
      </span>
    </div>
  );
}
