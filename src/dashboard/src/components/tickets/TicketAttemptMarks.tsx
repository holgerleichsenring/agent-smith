import type { TicketAttemptMark } from "@/types/tickets";

// 2026-10-13-9a59p: the mock's .attempts — one mark per attempt, oldest first. An attempt a reaper
// ended (endedOnInfrastructure) is drawn stopped, never failed: the work did not fail, the
// machine under it did.

export type MarkKind = "handed-back" | "asked" | "failed" | "ok" | "running" | "queued" | "stopped";

const MARK_CLASS: Record<MarkKind, string> = {
  "handed-back": "hb",
  asked: "hb",
  failed: "bad",
  ok: "ok",
  running: "run",
  queued: "",
  stopped: "stop",
};

export function markKind(mark: TicketAttemptMark): MarkKind {
  if (mark.handedBack) return "handed-back";
  if (mark.endedOnInfrastructure) return "stopped";
  switch (mark.status) {
    case "success":
    case "shortfall":
      return "ok";
    case "failed":
      return "failed";
    case "running":
      return "running";
    case "queued":
      return "queued";
    case "waiting_for_input":
      return "asked";
    default:
      return "stopped";
  }
}

/** 2026-11-22-9719b: the most marks a row draws; older ones fold into "+N" so a long history keeps its column. */
export const MAX_MARKS = 6; // the marks column is 76px wide

export function TicketAttemptMarks({ marks, count }: { marks: TicketAttemptMark[]; count: number }) {
  const shown = marks.slice(-MAX_MARKS);
  const folded = marks.length - shown.length;
  return (
    <div className="attempts hidesm" data-testid="ticket-attempts">
      {folded > 0 && <span className="al-more" title={`${folded} older attempts`} data-testid="ticket-attempts-folded">+{folded}</span>}
      {shown.map((mark) => {
        const kind = markKind(mark);
        return <i key={mark.runId} className={MARK_CLASS[kind]} data-mark={kind} title={`${mark.runId} · ${kind}`} />;
      })}
      <span className="al">{count}</span>
    </div>
  );
}
