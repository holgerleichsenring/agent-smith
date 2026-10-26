"use client";

import { useTicketDerivations, useTicketDetail } from "@/lib/tickets/TicketPageStore";
import type { TicketDerivation } from "@/types/tickets";
import { DeriveSpecsAct } from "./DeriveSpecsAct";
import { clock } from "./flowWords";

// 2026-10-26-bdd0f: the ticket's spec derivations, newest first, apart from its attempts — when, how long and what
// came of it: the run's own sentence, the spec pull requests, and Derive again on one that did not finish.

export function SpecDerivations() {
  const { project, ticketId } = useTicketDetail();
  const { derivations } = useTicketDerivations();
  if (derivations.length === 0) return null;
  return (
    <section className="derivations" data-testid="spec-derivations">
      <div className="section-head">
        <h2>Spec derivations</h2><span className="cnt">{derivations.length}</span>
        <span className="sh-sub">derived and reviewed without an attempt</span>
      </div>
      {derivations.map((d, i) => (
        <div key={d.runId} className="filed-line" data-testid={`spec-derivation-${d.runId}`}>
          <span><b>Spec derivation</b> · {clock(d.startedAt)}{took(d)}</span>
          <span data-testid={`spec-derivation-outcome-${d.runId}`}>{outcomeWords(d)}</span>
          {d.pullRequests.map((url) => <a key={url} href={url} target="_blank" rel="noreferrer">spec pull request ↗</a>)}
          {i === 0 && d.outcome === "not-finished" && <DeriveSpecsAct project={project} ticketId={ticketId} label="Derive again" />}
        </div>
      ))}
    </section>
  );
}

/** What the line says came of the derivation. */
export function outcomeWords(d: TicketDerivation): string {
  switch (d.outcome) {
    case "running": return "deriving…";
    case "derived": return d.summary ?? `${d.specCount} ${d.specCount === 1 ? "spec" : "specs"} derived`;
    case "asked": return d.summary ?? "stopped on a question, asked on the ticket";
    default: return `not finished${d.summary ? `: ${d.summary}` : ""}`;
  }
}

function took(d: TicketDerivation): string {
  if (!d.finishedAt) return "";
  const minutes = Math.max(1, Math.round((Date.parse(d.finishedAt) - Date.parse(d.startedAt)) / 60000));
  return ` · ${minutes} min`;
}
