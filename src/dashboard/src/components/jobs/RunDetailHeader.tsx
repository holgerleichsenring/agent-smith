"use client";

import Link from "next/link";
import type { HubConnectionState } from "@microsoft/signalr";
import { ConnectionState } from "@/components/jobs/ConnectionState";
import { CancelRunButton } from "@/components/jobs/CancelRunButton";
import { CancelRequestedBadge } from "@/components/jobs/CancelRequestedBadge";

// p0330: the states in which a cancel is actionable — running (cooperative or
// force-kill) AND queued (TryCancelQueuedAsync); the capacity-waiting run is
// exactly the one the operator most wants to kill.
// p0327: waiting_for_input is cancellable too — the parked run holds no
// compute, but the operator may decide the work is moot.
const CANCELLABLE_STATUSES = new Set(["running", "queued", "waiting_for_input"]);

// p0219: run-detail header. The PIPELINE (the trigger-tag taxonomy: fix-bug,
// add-feature, …) is the stable identity of a run, so it headlines as the h1.
// The ticketId + title are context, demoted to secondary metadata.

interface RunDetailHeaderProps {
  pipeline: string | null;
  ticketId: string | null;
  ticketTitle: string | null;
  runId: string;
  stepCaption: string | null;
  agentName: string | null;
  repoNames: string[];
  connectionState: HubConnectionState;
  // p0330: the header decides cancellability from the run STATUS (running or
  // queued show the button); cancelRequested is the durable persisted flag —
  // it flips the button to "cancelling…" and stays visible as a badge/hint
  // even once the run leaves the cancellable states.
  status: string | null;
  cancelRequested: boolean;
  // p0332: the run's cost line — LLM spend (money) and reserved capacity-time
  // (memory request × pod lifetime, Gi·min). The reserved figure is a
  // RESERVATION, not measured consumption and not money; the label must say
  // "reserved" and never imply actual cost.
  costUsd: number | null;
  reservedGiMinutes: number | null;
}

export function RunDetailHeader({
  pipeline,
  ticketId,
  ticketTitle,
  runId,
  stepCaption,
  agentName,
  repoNames,
  connectionState,
  status,
  cancelRequested,
  costUsd,
  reservedGiMinutes,
}: RunDetailHeaderProps) {
  const cancellable = CANCELLABLE_STATUSES.has((status ?? "").toLowerCase());
  const hasCost = costUsd !== null && costUsd > 0;
  const hasReserved = reservedGiMinutes !== null;
  return (
    <header className="flex items-start justify-between gap-4">
      <div className="space-y-1">
        <Link href="/" className="text-xs text-stone-500 hover:underline">← runs</Link>
        <h1 data-testid="run-heading" className="text-3xl font-medium tracking-tight">
          {pipeline ?? "run"}
        </h1>
        <div className="font-mono text-xs text-stone-400">
          {ticketId && (
            <span className="mr-2" data-testid="run-ticket-id">#{ticketId}</span>
          )}
          {ticketTitle && (
            <span className="mr-2" data-testid="run-ticket-title">{ticketTitle}</span>
          )}
          {runId}
          {stepCaption && <span className="ml-2">· {stepCaption}</span>}
          {agentName && (
            <span className="ml-2" data-testid="run-agent-name">· agent {agentName}</span>
          )}
        </div>
        {(hasCost || hasReserved) && (
          // p0332: the run's cost line. LLM spend is money; the reserved figure
          // is capacity-TIME (request × lifetime) — labeled "reserved", never
          // rendered as a $ amount.
          <div className="font-mono text-xs text-stone-400" data-testid="run-cost-line">
            {hasCost && (
              <span data-testid="run-cost-usd">${costUsd!.toFixed(2)} LLM</span>
            )}
            {hasCost && hasReserved && <span className="mx-1.5 text-stone-300">·</span>}
            {hasReserved && (
              <span
                data-testid="run-reserved-capacity"
                title="Reserved capacity-time: memory request × pod lifetime. A reservation, not measured usage."
              >
                reserved {reservedGiMinutes!.toFixed(1)} Gi·min
              </span>
            )}
          </div>
        )}
        {repoNames.length > 0 && (
          <div className="flex flex-wrap gap-1.5 pt-1">
            {repoNames.map((r) => (
              <code key={r} className="rounded bg-stone-100 px-1.5 py-0.5 font-mono dsh-label text-stone-700">
                {r}
              </code>
            ))}
          </div>
        )}
      </div>
      <div className="flex flex-none items-center gap-3">
        {cancellable ? (
          // The button itself reads "cancelling…" once the flag is set.
          <CancelRunButton runId={runId} cancelRequested={cancelRequested} />
        ) : (
          // No button any more, but a requested cancel stays visible: badge
          // while not yet terminal, muted hint if the run ended before the
          // cancel was enforced, nothing once the status itself is cancelled.
          <CancelRequestedBadge status={status ?? ""} cancelRequested={cancelRequested} />
        )}
        <ConnectionState state={connectionState} />
      </div>
    </header>
  );
}
