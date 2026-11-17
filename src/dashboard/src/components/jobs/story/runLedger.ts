import { EventType, type ProgressLedgerEntry, type RunEvent, type RunSnapshot } from "@/types/hub-events";

// 2026-11-17-d6fbb: the run's progress ledger, read by the story's building beat and by the run log's "Spec and plan"
// section alike.

/** The persisted ledger on the run detail, else the one the run's RunStoryRecorded event carries. */
export function runLedger(snapshot: RunSnapshot | null, events: RunEvent[]): ProgressLedgerEntry[] | null {
  return snapshot?.progressLedger ?? ledgerFromEvents(events);
}

// p0355: recover the persisted progress ledger from the run's RunStoryRecorded
// event when the snapshot did not carry it (list-snapshot window / failed detail
// fetch). The event's progressLedgerJson is the serialized ledger; normalize
// defensively across camelCase/PascalCase so a producer casing change can't turn
// a real ledger into a false "no ledger" state.
export function ledgerFromEvents(events: RunEvent[]): ProgressLedgerEntry[] | null {
  for (let i = events.length - 1; i >= 0; i--) {
    const e = events[i];
    if (e.type !== EventType.RunStoryRecorded) continue;
    if (!e.progressLedgerJson) return null;
    try {
      return normalizeLedger(JSON.parse(e.progressLedgerJson));
    } catch {
      return null;
    }
  }
  return null;
}

function normalizeLedger(raw: unknown): ProgressLedgerEntry[] | null {
  if (!Array.isArray(raw)) return null;
  const out: ProgressLedgerEntry[] = [];
  for (const r of raw) {
    if (!r || typeof r !== "object") continue;
    const o = r as Record<string, unknown>;
    const id = (o.id ?? o.Id) as string | undefined;
    const activity = (o.activity ?? o.Activity) as string | undefined;
    const rawStatus = (o.status ?? o.Status) as string | undefined;
    const target = (o.target ?? o.Target ?? null) as string | null;
    const status = normalizeLedgerStatus(rawStatus);
    if (!id || !activity || !status) continue;
    out.push({ id, activity, status, target });
  }
  return out.length > 0 ? out : null;
}

function normalizeLedgerStatus(s: string | undefined): ProgressLedgerEntry["status"] | null {
  switch ((s ?? "").toLowerCase().replace(/[^a-z]/g, "")) {
    case "done":
      return "done";
    case "inprogress":
      return "in_progress";
    case "pending":
      return "pending";
    default:
      return null;
  }
}

