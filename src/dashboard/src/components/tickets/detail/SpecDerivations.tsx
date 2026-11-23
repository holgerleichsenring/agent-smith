"use client";

import { useDateLocale, useT } from "@/hooks/useT";
import type { T } from "@/lib/i18n/translate";
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
  const t = useT();
  const locale = useDateLocale();
  const { project, ticketId } = useTicketDetail();
  const { derivations } = useTicketDerivations();
  if (derivations.length === 0) return null;
  return (
    <section className="derivations" data-testid="spec-derivations">
      <div className="section-head">
        <h2>{t("tickets.derive.title")}</h2><span className="cnt">{derivations.length}</span>
        <span className="sh-sub">{t("tickets.derive.sub")}</span>
      </div>
      {derivations.map((d, i) => (
        <div key={d.runId} className="filed-line" data-testid={`spec-derivation-${d.runId}`}>
          <span><b>{t("tickets.derive.one")}</b> · {clock(d.startedAt, locale)}{took(t, d)} · <span data-testid={`spec-derivation-cost-${d.runId}`}>{cost(t, d)}</span></span>
          <span data-testid={`spec-derivation-outcome-${d.runId}`}>{outcomeWords(t, d)}</span>
          {d.pullRequests.map((url) => <a key={url} href={url} target="_blank" rel="noreferrer">{t("tickets.derive.pullRequest")}</a>)}
          {i === 0 && d.outcome === "not-finished" && <DeriveSpecsAct project={project} ticketId={ticketId} label={t("tickets.derive.again")} />}
        </div>
      ))}
    </section>
  );
}

/** What the line says came of the derivation. */
export function outcomeWords(t: T, d: TicketDerivation): string {
  switch (d.outcome) {
    case "running": return t("tickets.derive.deriving");
    case "derived": return d.summary ?? t("tickets.derive.derived", { count: d.specCount });
    case "asked": return d.summary ?? t("tickets.derive.asked");
    default: return d.summary ? t("tickets.derive.notFinishedBecause", { reason: d.summary }) : t("tickets.derive.notFinished");
  }
}

/** " · 4m 10s" — how long it took, or has taken so far while it derives. */
function took(t: T, d: TicketDerivation): string {
  const span = elapsed(t, d.startedAt, d.finishedAt);
  return span ? ` · ${d.finishedAt ? span : t("tickets.cost.soFar", { amount: span })}` : "";
}

/** The derivation run's cost; "so far" while it is still deriving. */
function cost(t: T, d: TicketDerivation): string {
  return d.outcome === "running" ? t("tickets.cost.soFar", { amount: money(d.cost) }) : money(d.cost);
}
