"use client";

import Link from "next/link";
import type { HubConnectionState } from "@microsoft/signalr";
import { ConnectionState } from "@/components/jobs/ConnectionState";

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
}: RunDetailHeaderProps) {
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
      <ConnectionState state={connectionState} />
    </header>
  );
}
