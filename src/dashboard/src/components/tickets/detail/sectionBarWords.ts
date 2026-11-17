import { SERVER_BUCKET, TICKET_BUCKETS, type TicketAttempt, type TicketSummary } from "@/types/tickets";
import { TICKET_SECTIONS } from "../ticketSections";
import { platformWords, pullRequestFate } from "../ticketFormat";

// 2026-11-17-8d1ca: the words of the ticket's section bar — its state as the ticket list names that bucket (so the
// bar and the list it came from agree), and the facts at its foot, each from a recorded field only.

/** "In review", "Stuck", "Done" — the list's title of the ticket's bucket; null for a bucket the list does not know. */
export function stateWord(summary: TicketSummary): string | null {
  const bucket = TICKET_BUCKETS.find((b) => SERVER_BUCKET[b] === summary.bucket);
  return bucket ? TICKET_SECTIONS[bucket].title : null;
}

/** "Jira · In Review": the tracker and the status the newest attempt read; null when neither is recorded. */
export function trackerFact(summary: TicketSummary): string | null {
  if (!summary.platform && !summary.trackerState) return null;
  return [platformWords(summary.platform), summary.trackerState].filter(Boolean).join(" · ");
}

/** "1 PR merged, 1 open"; "2 PRs" when the attempts opened some whose state is not read yet; else "no PR". */
export function pullRequestFact(summary: TicketSummary, attempts: readonly TicketAttempt[] = []): string {
  const fate = pullRequestFate(summary.pullRequests ?? []);
  if (fate) return fate;
  const opened = new Set(attempts.flatMap((a) => a.pullRequests.flatMap((p) => (p.url ? [p.url] : [])))).size;
  if (opened === 0) return "no PR";
  return opened === 1 ? "1 PR" : `${opened} PRs`;
}
