import type { TicketPullRequest, TicketSeriesStrip } from "@/types/tickets";

// 2026-10-13-9a59p: the mock's .strip — the derivation cell, one cell per spec of the ticket's
// current series (2026-10-13-9a59e's seriesStrip), then one cell per pull request drawn from the
// row's pullRequests[] and never stored in the strip. Not the run's five beats: a beat cannot say
// carried, asked or handed back.

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

export function TicketStrip({
  strip,
  pullRequests,
}: {
  strip: TicketSeriesStrip | null | undefined;
  pullRequests: TicketPullRequest[];
}) {
  if (!strip && pullRequests.length === 0) return <div className="strip hidesm" />;
  return (
    <div className="strip hidesm" data-testid="ticket-strip">
      {strip && (
        <>
          <i className={`d ${DERIVE_CLASS[strip.derive] ?? ""}`.trim()} data-derive={strip.derive} />
          {strip.cells.map((cell) => (
            <i
              key={cell.specId}
              className={CELL_CLASS[cell.state] ?? ""}
              data-cell={cell.state}
              title={`${cell.specId} · ${cell.state.replace(/_/g, " ")}`}
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
        />
      ))}
    </div>
  );
}
