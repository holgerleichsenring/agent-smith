"use client";

import { useT } from "@/hooks/useT";
import { isRunLive } from "@/lib/runLiveness";
import type { AttemptMachines } from "@/lib/tickets/TicketPageStore";
import type { TicketAttempt, TicketDerivation, TicketSeriesBudget } from "@/types/tickets";
import { money, ticketCost } from "../ticketFormat";
import { costBuckets, costParts, specLabel } from "./costScale";
import { share } from "./seriesWords";

// 2026-10-13-9a59s: the Cost section — what each attempt cost, split by 9a59f's passes read, carried specs at $0.
// 2026-10-26-7000b: the ticket's spec derivations on a line of their own, not an attempt, and in the total.
// 2026-11-17-8d1cc: plain (TicketRail mock) — the total against the series budget with one thin bar, then one mono line
// per run: its cost, its split, its LLM calls and its own cap. The per-attempt bars on one scale are gone.

interface Props {
  attempts: TicketAttempt[];
  derivations: TicketDerivation[];
  machines: AttemptMachines;
  ordinals: Readonly<Record<string, number>>;
  budget: TicketSeriesBudget | null | undefined;
}

export function CostSection({ attempts, derivations, machines, ordinals, budget }: Props) {
  const t = useT();
  const total = ticketCost(attempts, derivations);
  const capped = budget && budget.heldBecause !== "off" ? budget : null;
  return (
    <>
      <div className="tk-cost-head">
        <span className="tk-cost-total" data-testid="cost-total">{money(total)}</span>
        <span className="tk-dim" data-testid="cost-budget">
          {capped ? t("tickets.cost.budget", { spent: money(capped.spentUsd), cap: money(capped.capUsd) }) : t("tickets.cost.spent")}
        </span>
      </div>
      {capped && (
        <div className="tk-cost-bar" aria-hidden="true" data-testid="cost-budget-bar">
          <span className={capped.spentUsd >= capped.capUsd ? "over" : undefined} style={{ width: `${share(capped.spentUsd, capped.capUsd) * 100}%` }} />
        </div>
      )}
      <div className="tk-cost-lines" data-testid="cost-rows">
        {attempts.map((attempt) => {
          const live = isRunLive(attempt.status);
          const run = machines.run[attempt.runId]?.data ?? null;
          const buckets = costBuckets(t, machines.passes[attempt.runId]?.data ?? null, ordinals);
          const carried = (attempt.revision?.carriedIds ?? []).map((id) => specLabel(id, ordinals));
          const figures = [run ? t("tickets.cost.llmCalls", { count: run.llmCalls }) : null, run?.budgetCapUsd ? t("tickets.cost.runCap", { cap: money(run.budgetCapUsd) }) : null]
            .filter(Boolean).join(" · ");
          return (
            <div key={attempt.runId} className="tk-cost-line" data-testid={`cost-row-${attempt.number}`}>
              <span className="tk-cost-who">{t("tickets.cost.run", { n: attempt.number })}</span>
              <span className="tk-cost-usd">{money(attempt.cost)}</span>
              <span className="tk-dim">
                <span data-testid={`cost-parts-${attempt.number}`}>{costParts(t, buckets, carried, live) || (live ? t("tickets.cost.soFarWord") : t("tickets.cost.noSplit"))}</span>
                {figures && <span data-testid={`cost-figures-${attempt.number}`}> · {figures}</span>}
              </span>
            </div>
          );
        })}
        <DerivationsLine derivations={derivations} />
      </div>
    </>
  );
}

/** The spec derivations summed on one line; none, no line. */
function DerivationsLine({ derivations }: { derivations: TicketDerivation[] }) {
  const t = useT();
  if (derivations.length === 0) return null;
  const running = derivations.some((d) => d.outcome === "running");
  return (
    <div className="tk-cost-line" data-testid="cost-derivations">
      <span className="tk-cost-who">{t("tickets.cost.derivations")}</span>
      <span className="tk-cost-usd">{money(derivations.reduce((sum, d) => sum + d.cost, 0))}</span>
      <span className="tk-dim">{t("tickets.cost.derivationCount", { count: derivations.length })}{running ? ` · ${t("tickets.cost.soFarWord")}` : ""}</span>
    </div>
  );
}
