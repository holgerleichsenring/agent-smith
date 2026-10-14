"use client";

import { Fragment } from "react";
import Link from "next/link";
import type { TicketAttempt, TicketSeriesBudget } from "@/types/tickets";
import { money } from "../ticketFormat";
import { SERIES_SETTINGS_PATH, chainOf, heldWords, hours, nextWords, share } from "./seriesWords";

// 2026-10-13-8762w: the series strip between the header and the tabs — spend and summed run time of the chain
// against the series caps, the chained attempts (an automatic start marked "auto") and whether the next attempt
// starts by itself, holds and why, or the series is complete. It shows what the server decided and fetches
// nothing: the page store's default series read carries the budget. A one-chunk ticket or a turned-off budget
// has no strip.

interface Props {
  budget: TicketSeriesBudget | null | undefined;
  attempts: TicketAttempt[];
  /** Selects the Defaults tab. */
  onDefaults: () => void;
}

export function SeriesRunway({ budget, attempts, onDefaults }: Props) {
  if (!budget || budget.heldBecause === "off") return null;
  const links = chainOf(attempts, budget.chainFrom);
  const held = budget.next === "hold" ? heldWords(budget.heldBecause) : null;
  return (
    <div className="sr" data-testid="series-runway" data-next={budget.next}>
      <span className="sr-k">Series</span>
      <Meter testId="series-spend" value={budget.spentUsd} cap={budget.capUsd}
        text={`${money(budget.spentUsd)} of ${money(budget.capUsd)}`} />
      <Meter testId="series-time" value={budget.elapsedHours} cap={budget.capHours}
        text={`${hours(budget.elapsedHours)} of ${hours(budget.capHours)} run time`} />
      {links.length > 0 && (
        <span className="sr-chain" data-testid="series-chain">
          {links.map((link, i) => (
            <Fragment key={link.number}>
              {i > 0 && (
                <span className="sr-arrow" data-auto={link.auto ? "true" : undefined}>
                  {" → "}{link.auto && <i className="sr-auto">auto</i>}
                </span>
              )}
              <span data-testid={`series-chain-a${link.number}`}>A{link.number}</span>
            </Fragment>
          ))}
        </span>
      )}
      <span className={`sr-next ${budget.next}`} data-testid="series-next">
        {nextWords(budget)}
        {held && <>. <GoOn words={held} onDefaults={onDefaults} /></>}
      </span>
    </div>
  );
}

function Meter({ value, cap, text, testId }: { value: number; cap: number; text: string; testId: string }) {
  const hot = value >= cap;
  return (
    <span className={hot ? "sr-m hot" : "sr-m"} data-testid={testId} data-hot={hot ? "true" : "false"}>
      <span className="sr-bar" aria-hidden="true"><span style={{ width: `${share(value, cap) * 100}%` }} /></span>
      {text}
    </span>
  );
}

function GoOn({ words, onDefaults }: { words: ReturnType<typeof heldWords>; onDefaults: () => void }) {
  if (words.to === "defaults") {
    return (
      <button type="button" className="sr-go" data-testid="series-go-defaults" aria-controls="tab-defaults"
        onClick={onDefaults}>{words.goOn}</button>
    );
  }
  if (words.to === "settings") {
    return <Link className="sr-go" href={SERIES_SETTINGS_PATH} data-testid="series-go-settings">{words.goOn}</Link>;
  }
  return <span data-testid="series-go-on">{words.goOn}</span>;
}
