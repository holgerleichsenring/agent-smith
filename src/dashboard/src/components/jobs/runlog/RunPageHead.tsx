"use client";

import { Fragment } from "react";
import Link from "next/link";
import type { HubConnectionState } from "@microsoft/signalr";
import { ConnectionState } from "@/components/jobs/ConnectionState";
import { CancelRequestedBadge } from "@/components/jobs/CancelRequestedBadge";
import { CancelRunButton } from "@/components/jobs/CancelRunButton";
import { DeleteRunButton } from "@/components/jobs/DeleteRunButton";
import { useSandboxActivity } from "@/hooks/useSandboxActivity";
import { RUNS_PATH } from "@/lib/RunBucketFilter";
import type { RunSnapshot } from "@/types/hub-events";
import { computeOf } from "./runFactWords";
import { detailLines, metaParts } from "./runMetaWords";
import type { RunTicket } from "./useRunTicket";

// 2026-11-16-6877b: the run page's header — the way back, the title and the run's own acts: cancel while it can be
// stopped, delete always.
// 2026-11-17-6d9ea: in the approved mock's shape — the crumb "Runs / run id · ticket · Run n of m", the title, ONE meta
// line (state in its colour · steps · elapsed · cost of its cap · pods · memory · shape), and every other recorded fact
// behind "details": when, the model, the repositories, the pipeline, the time split, the budget share, the shape's
// reason, the sandbox beat and the pods.

const CANCELLABLE = new Set(["running", "queued", "waiting_for_input"]);

interface Props {
  runId: string;
  snapshot: RunSnapshot | null;
  ticket: RunTicket | null;
  repoNames: string[];
  /** Steps the run reached, for the meta line once it has ended. */
  stepCount: number;
  /** The live connection, said beside the acts when it is not connected. */
  connectionState?: HubConnectionState;
  onDeleted: () => void;
}

export function RunPageHead({ runId, snapshot, ticket, repoNames, stepCount, connectionState, onDeleted }: Props) {
  const status = (snapshot?.status ?? "").toLowerCase();
  const title = snapshot?.ticketTitle ?? snapshot?.pipeline ?? "run";
  const beat = useSandboxActivity(snapshot?.runId ?? null);
  const parts = snapshot ? metaParts(snapshot, stepCount) : [];
  const details = snapshot ? detailLines(snapshot, repoNames, beat) : [];
  const pods = snapshot ? computeOf(snapshot) : null;
  return (
    <header className="rl-head" data-testid="run-head">
      <nav className="rl-crumb" aria-label="Breadcrumb">
        <Link href={RUNS_PATH}>Runs</Link>
        <span aria-hidden="true">/</span>
        <span className="rl-mono" data-testid="run-crumb-run" title={runId}>{runId}</span>
        {ticket && (
          <>
            <span aria-hidden="true">·</span>
            <Link href={ticket.href} data-testid="run-crumb-ticket">ticket {ticket.label}</Link>
            {ticket.number !== null && ticket.of !== null && (
              <>
                <span aria-hidden="true">·</span>
                <span data-testid="run-crumb-count">Run {ticket.number} of {ticket.of}</span>
              </>
            )}
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
      {parts.length > 0 && (
        <div className="rl-meta" data-testid="run-meta">
          {parts.map((p, i) => (
            <Fragment key={p.key}>
              {i > 0 && " · "}
              <span data-testid={`run-meta-${p.key}`} data-tone={p.tone}>{p.text}</span>
            </Fragment>
          ))}
        </div>
      )}
      {details.length > 0 && (
            <details className="rl-details" data-testid="run-details">
              <summary>details</summary>
              {details.map((d) => <div key={d} data-testid="run-detail-line">{d}</div>)}
              {pods && pods.pods.length > 0 && (
                <>
                  <div className="rl-dim">{pods.heading}</div>
                  {pods.pods.map((pod) => (
                    <div key={pod.name} data-testid="run-detail-pod">{pod.name} · {pod.resources} · <span className="rl-dim">{pod.image}</span></div>
                  ))}
                </>
              )}
            </details>
      )}
    </header>
  );
}
