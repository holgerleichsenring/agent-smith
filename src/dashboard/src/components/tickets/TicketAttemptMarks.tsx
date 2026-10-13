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

export function TicketAttemptMarks({ marks, count }: { marks: TicketAttemptMark[]; count: number }) {
  return (
    <div className="attempts hidesm" data-testid="ticket-attempts">
      {marks.map((mark) => {
        const kind = markKind(mark);
        return <i key={mark.runId} className={MARK_CLASS[kind]} data-mark={kind} title={`${mark.runId} · ${kind}`} />;
      })}
      <span className="al">{count}</span>
    </div>
  );
}
