"use client";

import Link from "next/link";
import type { HubConnectionState } from "@microsoft/signalr";
import { ConnectionState } from "@/components/jobs/ConnectionState";
import { CancelRequestedBadge } from "@/components/jobs/CancelRequestedBadge";
import { CancelRunButton } from "@/components/jobs/CancelRunButton";
import { DeleteRunButton } from "@/components/jobs/DeleteRunButton";
import { RUNS_PATH } from "@/lib/RunBucketFilter";
import { shortRunId } from "@/lib/runId";
import type { RunSnapshot } from "@/types/hub-events";
import { computeOf, costWords } from "./runFactWords";
import { sectionDuration } from "./runSections";
import type { RunTicket } from "./useRunTicket";

// 2026-11-16-6877b: the run page's header — the way back to the ticket ("GH-101 / Run 2 of 2"), the title, one line
// of what is recorded (when, how long, the model, the containers, what it cost) and the run's own acts: cancel while
// it can be stopped, delete always.

const CANCELLABLE = new Set(["running", "queued", "waiting_for_input"]);

interface Props {
  runId: string;
  snapshot: RunSnapshot | null;
  ticket: RunTicket | null;
  repoNames: string[];
  /** The live connection, said beside the acts when it is not connected. */
  connectionState?: HubConnectionState;
  onDeleted: () => void;
}

export function RunPageHead({ runId, snapshot, ticket, repoNames, connectionState, onDeleted }: Props) {
  const status = (snapshot?.status ?? "").toLowerCase();
  const title = snapshot?.ticketTitle ?? snapshot?.pipeline ?? "run";
  return (
    <header className="rl-head" data-testid="run-head">
      <nav className="rl-crumb" aria-label="Breadcrumb">
        {ticket ? (
          <>
            <Link href={ticket.href} data-testid="run-crumb-ticket">{ticket.label}</Link>
            <span aria-hidden="true">/</span>
            <span data-testid="run-crumb-run">{ticket.number !== null && ticket.of !== null ? `Run ${ticket.number} of ${ticket.of}` : `Run ${shortRunId(runId)}`}</span>
          </>
        ) : (
          <>
            <Link href={RUNS_PATH}>Runs</Link>
            <span aria-hidden="true">/</span>
            <span data-testid="run-crumb-run" title={runId}>Run {shortRunId(runId)}</span>
          </>
        )}
      </nav>
      <div className="rl-title-row">
        <h1 data-testid="run-heading" title={title}>{title}</h1>
        <span className="rl-acts" data-testid="run-header-actions">
          {connectionState !== undefined && <ConnectionState state={connectionState} />}
          {CANCELLABLE.has(status)
            ? <CancelRunButton runId={runId} cancelRequested={snapshot?.cancelRequested ?? false} />
            : <CancelRequestedBadge status={status} cancelRequested={snapshot?.cancelRequested ?? false} />}
          <DeleteRunButton runId={runId} onDeleted={onDeleted} />
        </span>
      </div>
      <div className="rl-meta" data-testid="run-meta">{metaLine(snapshot, repoNames).join(" · ")}</div>
    </header>
  );
}

/** When, how long, the model, the repositories and compute, the cost against its cap, the shape, the pipeline and
 *  the run id; each only when recorded. */
export function metaLine(snapshot: RunSnapshot | null, repoNames: string[]): string[] {
  if (!snapshot) return [];
  const parts: string[] = [];
  const start = new Date(snapshot.startedAt);
  if (!Number.isNaN(start.getTime())) {
    const day = start.toLocaleDateString("en-GB", { day: "numeric", month: "short" });
    const time = (iso: string) => new Date(iso).toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit", hour12: false });
    parts.push(snapshot.finishedAt ? `${day}, ${time(snapshot.startedAt)} to ${time(snapshot.finishedAt)}` : `${day}, since ${time(snapshot.startedAt)}`);
    const end = snapshot.finishedAt ? Date.parse(snapshot.finishedAt) : Date.now();
    const took = sectionDuration((end - start.getTime()) / 1000);
    if (took) parts.push(took);
  }
  if (snapshot.agentName) parts.push(snapshot.llmCalls > 0 ? `${snapshot.agentName}, ${snapshot.llmCalls} model calls` : snapshot.agentName);
  if (repoNames.length > 0) parts.push(repoNames.join(", "));
  const compute = computeOf(snapshot);
  if (compute) parts.push(compute.words);
  else if (snapshot.sandboxes > 0) parts.push(snapshot.sandboxes === 1 ? "1 container" : `${snapshot.sandboxes} containers`);
  const cost = costWords(snapshot);
  if (cost) parts.push(cost);
  if (snapshot.reservedGiMinutes != null) parts.push(`reserved ${snapshot.reservedGiMinutes.toFixed(1)} Gi·min`);
  // 2026-11-17-d6fbb: what the graph view's side rail carried — the shape that decided the process, the pipeline
  // and the full run id.
  if (snapshot.workShape) parts.push(`shape ${snapshot.workShape}`);
  if (snapshot.pipeline) parts.push(`pipeline ${snapshot.pipeline}`);
  parts.push(`run ${snapshot.runId}`);
  return parts;
}
