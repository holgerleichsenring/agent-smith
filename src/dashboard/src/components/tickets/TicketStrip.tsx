"use client";

import { useT } from "@/hooks/useT";
import { messages } from "@/lib/i18n/messages";
import type { T, TextKey } from "@/lib/i18n/translate";
import type { TicketPullRequest, TicketSeriesStrip } from "@/types/tickets";

// 2026-10-13-9a59p: the mock's .strip — the derivation cell, one cell per spec of the ticket's
// current series (2026-10-13-9a59e's seriesStrip), then one cell per pull request drawn from the
// row's pullRequests[] and never stored in the strip. Not the run's five beats: a beat cannot say
// carried, asked or handed back.
// 2026-11-22-50d7a: the strip says what it is (the newest attempt's progress) and each cell its state in words.

const DERIVE_CLASS: Record<string, string> = { cut: "done", deriving: "live", handed_back: "ask" };

const CELL_CLASS: Record<string, string> = {
  done: "done",
  carried: "carried",
  live: "live",
  asked: "ask",
  failed: "bad",
  handed_back: "ask",
  not_started: "",
};

/** The PR cell's class from the record's exact state and decision strings. */
export function prCellClass(pr: TicketPullRequest): string {
  if (pr.state === "merged") return "p done";
  if (pr.state === "closed") return "p pr ended";
  if (pr.state !== "open") return "p"; // unsupported / unread: no review state to draw
  switch (pr.decision) {
    case "approved":
      return "p pr appr";
    case "changes-requested":
    case "rejected":
      return "p pr chg";
    case "changes-requested-untrusted":
      return "p pr untrusted";
    default:
      return "p pr";
  }
}

export function prCellTitle(pr: TicketPullRequest): string {
  const decision = pr.decision === "none" ? "" : ` · ${pr.decision.replace(/-/g, " ")}`;
  return `PR #${pr.number} ${pr.state}${decision}${pr.by ? ` by ${pr.by}` : ""}`;
}

/** A state word from the dictionary, the raw state where none is defined. */
function stateWords(t: T, prefix: string, state: string): string {
  const key = `${prefix}.${state}`;
  return Object.prototype.hasOwnProperty.call(messages, key) ? t(key as TextKey) : state.replace(/_/g, " ");
}

export function deriveTitle(t: T, derive: string): string {
  return t("tickets.strip.derive", { state: stateWords(t, "tickets.strip.derive", derive) });
}

export function cellTitle(t: T, specId: string, state: string): string {
  return t("tickets.strip.cell", { spec: specId, state: stateWords(t, "tickets.strip.cell", state) });
}

export function TicketStrip({
  strip,
  pullRequests,
}: {
  strip: TicketSeriesStrip | null | undefined;
  pullRequests: TicketPullRequest[];
}) {
  const t = useT();
  if (!strip && pullRequests.length === 0) return <div className="strip hidesm" />;
  const label = t("tickets.strip.label");
  return (
    <div className="strip hidesm" data-testid="ticket-strip" role="group" aria-label={label} title={label}>
      {strip && (
        <>
          <i className={`d ${DERIVE_CLASS[strip.derive] ?? ""}`.trim()} data-derive={strip.derive}
            title={deriveTitle(t, strip.derive)} aria-label={deriveTitle(t, strip.derive)} role="img" />
          {strip.cells.map((cell) => (
            <i
              key={cell.specId}
              className={CELL_CLASS[cell.state] ?? ""}
              data-cell={cell.state}
              title={cellTitle(t, cell.specId, cell.state)}
              aria-label={cellTitle(t, cell.specId, cell.state)}
              role="img"
            />
          ))}
        </>
      )}
      {pullRequests.map((pr) => (
        <i
          key={pr.url}
          className={prCellClass(pr)}
          data-pr-state={pr.state}
          data-pr-decision={pr.decision}
          title={prCellTitle(pr)}
          aria-label={prCellTitle(pr)}
          role="img"
        />
      ))}
    </div>
  );
}
