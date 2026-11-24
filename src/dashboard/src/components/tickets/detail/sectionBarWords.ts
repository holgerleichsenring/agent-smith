import type { T } from "@/lib/i18n/translate";
import { SERVER_BUCKET, TICKET_BUCKETS, type TicketAttempt, type TicketPullRequest, type TicketSummary } from "@/types/tickets";
import { TICKET_SECTIONS } from "../ticketSections";
import { platformWords, pullRequestFate } from "../ticketFormat";

// 2026-11-17-8d1ca: the words of the ticket's section bar — its state as the ticket list names that bucket (so the
// bar and the list it came from agree), and the facts at its foot, each from a recorded field only.

/** "In review", "Stuck", "Done" — the list's title of the ticket's bucket; null for a bucket the list does not know. */
export function stateWord(t: T, summary: TicketSummary): string | null {
  const bucket = TICKET_BUCKETS.find((b) => SERVER_BUCKET[b] === summary.bucket);
  return bucket ? t(TICKET_SECTIONS[bucket].title) : null;
}

/** "Jira status In Review": the tracker and the status the newest attempt read there — labelled, so it never reads as the
 *  ticket's own state beside it (2026-11-18-d6d2e); "tracker status New" without a platform, "Jira" without a status;
 *  null when neither is recorded. */
export function trackerFact(t: T, summary: TicketSummary): string | null {
  if (!summary.platform && !summary.trackerState) return null;
  const tracker = summary.platform ? platformWords(t, summary.platform) : t("tickets.bar.tracker");
  return summary.trackerState ? t("tickets.bar.trackerStatus", { tracker, status: summary.trackerState }) : tracker;
}

/** "1 PR merged, 1 open"; "2 PRs" when the attempts opened some whose state is not read yet; else "no PR". 2026-11-17-3435a:
 *  an open pull request's CI follows, as "CI red: build, lint", "CI pending" or "CI green". */
export function pullRequestFact(t: T, summary: TicketSummary, attempts: readonly TicketAttempt[] = []): string {
  const fate = pullRequestFate(t, summary.pullRequests ?? []);
  const ci = ciWords(t, summary.pullRequests ?? []);
  if (fate) return ci ? `${fate} · ${ci}` : fate;
  const opened = new Set(attempts.flatMap((a) => a.pullRequests.flatMap((p) => (p.url ? [p.url] : [])))).size;
  if (opened === 0) return t("tickets.bar.noPr");
  return t("tickets.bar.prs", { count: opened });
}

/** 2026-11-17-3435a: the CI of the open pull requests in one phrase — red names its failed checks; null when no open
 *  pull request has a CI read or none reports CI. */
export function ciWords(t: T, pullRequests: readonly TicketPullRequest[]): string | null {
  const read = pullRequests.filter((p) => p.state === "open" && p.ci && p.ci.state !== "none").map((p) => p.ci!);
  if (read.length === 0) return null;
  const red = read.filter((c) => c.state === "red");
  if (red.length > 0) {
    const names = red.flatMap((c) => c.failures.map((f) => f.name));
    return names.length > 0 ? t("tickets.bar.ciRedNames", { names: [...new Set(names)].join(", ") }) : t("tickets.bar.ciRed");
  }
  // 2026-11-23-0219: a required build that never started is no green CI and no running one.
  if (read.some((c) => c.state === "not_started")) return t("tickets.bar.buildNotStarted");
  return read.some((c) => c.state === "pending") ? t("tickets.bar.ciPending") : t("tickets.bar.ciGreen");
}
