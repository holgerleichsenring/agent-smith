"use client";

import Link from "next/link";
import type { HubConnectionState } from "@microsoft/signalr";
import { ConnectionState } from "@/components/jobs/ConnectionState";
import { CancelRunButton } from "@/components/jobs/CancelRunButton";
import { CancelRequestedBadge } from "@/components/jobs/CancelRequestedBadge";
import { DeleteRunButton } from "@/components/jobs/DeleteRunButton";

// p0330: the states in which a cancel is actionable — running (cooperative or
// force-kill) AND queued (TryCancelQueuedAsync); waiting_for_input too.
const CANCELLABLE_STATUSES = new Set(["running", "queued", "waiting_for_input"]);

// p0343c (pixel identity): the run-viewer.html header verbatim — "‹ All runs"
// back link, the ticket title as h1, the .statusline with the .spill state dot
// + phrase (plus the real cancel/delete actions and connection state), and the
// .ident joined field-block strip on the right (Run # / Ticket / Pipeline /
// Agent / Repositories — each field renders ONLY when the snapshot carries it).

export function statusSpill(status: string | null): { cls: string; label: string } {
  switch ((status ?? "").toLowerCase()) {
    case "running":
      return { cls: "is-run", label: "Running" };
    case "waiting_for_input":
      return { cls: "is-blocked", label: "Needs you" };
    case "queued":
      return { cls: "is-prov", label: "Queued" };
    case "success":
      return { cls: "is-done", label: "Done" };
    case "cancelled":
      return { cls: "", label: "Cancelled" };
    case "":
      return { cls: "", label: "…" };
    default:
      return { cls: "is-failed", label: "Failed" };
  }
}

interface RunDetailHeaderProps {
  pipeline: string | null;
  ticketId: string | null;
  ticketTitle: string | null;
  runId: string;
  /** The real status phrase for the spill line (e.g. the current step). */
  phrase: string | null;
  agentName: string | null;
  repoNames: string[];
  connectionState: HubConnectionState;
  status: string | null;
  cancelRequested: boolean;
  // p0332: reserved capacity-time (memory request × pod lifetime, Gi·min) — a
  // RESERVATION, never rendered as money.
  costUsd: number | null;
  reservedGiMinutes: number | null;
  onDeleted?: () => void;
}

export function RunDetailHeader({
  pipeline,
  ticketId,
  ticketTitle,
  runId,
  phrase,
  agentName,
  repoNames,
  connectionState,
  status,
  cancelRequested,
  costUsd,
  reservedGiMinutes,
  onDeleted,
}: RunDetailHeaderProps) {
  const cancellable = CANCELLABLE_STATUSES.has((status ?? "").toLowerCase());
  const spill = statusSpill(status);
  const hasReserved = reservedGiMinutes !== null;

  return (
    <header>
      <Link className="back" href="/">
        <svg width="13" height="13" viewBox="0 0 16 16" fill="none" aria-hidden="true">
          <path
            d="M9.5 3.5L5 8l4.5 4.5"
            stroke="currentColor"
            strokeWidth="1.6"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
        </svg>
        All runs
      </Link>
      <div className="head-row">
        <div className="head-main">
          <h1 data-testid="run-heading">{ticketTitle ?? pipeline ?? "run"}</h1>
          <div className="statusline">
            <span className="spill">
              <span className="d" />
              <span data-testid="run-status-spill">{spill.label}</span>
            </span>
            {phrase && <span className="phrase">— {phrase}</span>}
            {/* p0345b: the run's actions stay on the header — never tucked away. */}
            {cancellable ? (
              <CancelRunButton runId={runId} cancelRequested={cancelRequested} />
            ) : (
              <CancelRequestedBadge status={status ?? ""} cancelRequested={cancelRequested} />
            )}
            <DeleteRunButton runId={runId} onDeleted={onDeleted} />
            <ConnectionState state={connectionState} />
          </div>
          {hasReserved && (
            <div className="repos-inline" style={{ marginTop: 6 }} data-testid="run-cost-line">
              <span
                data-testid="run-reserved-capacity"
                title="Reserved capacity-time: memory request × pod lifetime. A reservation, not measured usage."
              >
                reserved {reservedGiMinutes!.toFixed(1)} Gi·min
              </span>
              {costUsd !== null && costUsd > 0 && (
                <>
                  {" · "}
                  <span data-testid="run-cost-usd">${costUsd.toFixed(2)} LLM</span>
                </>
              )}
            </div>
          )}
        </div>
        <div className="ident" data-testid="run-ident">
          <div className="f">
            <span className="fl">Run</span>
            <span className="fv" title={runId}>
              #{runId.length > 10 ? runId.slice(0, 8) : runId}
            </span>
          </div>
          {ticketId && (
            <div className="f">
              <span className="fl">Ticket</span>
              <span className="fv" data-testid="run-ticket-id">{ticketId}</span>
            </div>
          )}
          {pipeline && (
            <div className="f">
              <span className="fl">Pipeline</span>
              <span className="fv">{pipeline}</span>
            </div>
          )}
          {agentName && (
            <div className="f">
              <span className="fl">Agent</span>
              <span className="fv" data-testid="run-agent-name">{agentName}</span>
            </div>
          )}
          {repoNames.length > 0 && (
            <div className="f">
              <span className="fl">Repositories</span>
              <span className="fv" data-testid="run-repos" title={repoNames.join(", ")}>
                {repoNames.length === 1 ? repoNames[0] : `${repoNames.length} · ${repoNames[0]}`}
              </span>
            </div>
          )}
        </div>
      </div>
    </header>
  );
}
