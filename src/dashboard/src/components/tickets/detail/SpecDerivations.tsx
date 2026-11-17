"use client";

import { useTicketDerivations, useTicketDetail } from "@/lib/tickets/TicketPageStore";
import type { TicketDerivation } from "@/types/tickets";
import { money } from "../ticketFormat";
import { DeriveSpecsAct } from "./DeriveSpecsAct";
import { clock, elapsed } from "./flowWords";

// 2026-10-26-bdd0f: the ticket's spec derivations, newest first, apart from its attempts — when, how long and what
// came of it: the run's own sentence, the spec pull requests, and Derive again on one that did not finish.
// 2026-10-26-7000a: and what it cost, written as an attempt's cost is. 2026-11-17-8d1cd: shown in the Specs section of a
// ticket with runs as well; its duration reads like a run's ("4m 10s").

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
          <span><b>Spec derivation</b> · {clock(d.startedAt)}{took(d)} · <span data-testid={`spec-derivation-cost-${d.runId}`}>{cost(d)}</span></span>
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

/** " · 4m 10s" — how long it took, or has taken so far while it derives. */
function took(d: TicketDerivation): string {
  const span = elapsed(d.startedAt, d.finishedAt);
  return span ? ` · ${span}${d.finishedAt ? "" : " so far"}` : "";
}

/** The derivation run's cost; "so far" while it is still deriving. */
function cost(d: TicketDerivation): string {
  return d.outcome === "running" ? `${money(d.cost)} so far` : money(d.cost);
}
