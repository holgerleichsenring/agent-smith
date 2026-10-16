import { runPath } from "@/lib/runPath";
import { EventType, type RunEvent, type StepStartedEvent } from "@/types/hub-events";
import { RUN_END_KINDS, type TicketAttempt, type TicketDetail, type TicketSeries } from "@/types/tickets";
import { closedWords } from "../TicketActivity";
import { liveSpec } from "./attemptStory";
import { prNumber } from "./conversationWords";

// 2026-10-16-86e6c: the ticket state block's words — what is going on (one sentence and a tone), what to do (ONE act,
// other options behind a disclosure) and which run the sentence is about, so the way to it and to its verbatim cause
// stays in sight. The act is chosen from recorded fields only: the bucket and its kind, endedOnInfrastructure and the
// attempt's endKind. No summary text is matched; a cause without a field falls to Continue in conversation.

export type StateTone = "ok" | "bad" | "amber" | "person" | "q";

export type StateAct =
  | { kind: "retry"; attempt: TicketAttempt }
  | { kind: "init"; project: string }
  | { kind: "link"; label: string; href: string; note?: string }
  | { kind: "tab"; label: string; tab: string; note?: string };

export interface TicketStateView {
  tone: StateTone;
  pill: string;
  sentence: string;
  /** The question, the hand-back, the live step or the pull requests — drawn under the sentence. */
  detail: string | null;
  /** The attempt the sentence is about: its run is linked beside the sentence. */
  attempt: TicketAttempt | null;
  act: StateAct | null;
  others: StateAct[];
  /** Healthy: one line, no disclosures. */
  quiet: boolean;
}

export interface TicketStateInput {
  detail: TicketDetail;
  liveEvents: RunEvent[];
  /** The ticket's default cut — the live attempt's spec and step when it is that attempt's own. */
  series: TicketSeries | null;
}

export const CONTINUE: StateAct = { kind: "tab", label: "Continue in conversation", tab: "conversation" };
const DISCUSS: StateAct = { kind: "tab", label: "Discuss in conversation", tab: "conversation" };
export const SANDBOX_SETTINGS_PATH = "/config/settings/sandbox";

const base = (attempt: TicketAttempt | null): Omit<TicketStateView, "tone" | "pill" | "sentence"> =>
  ({ detail: null, attempt, act: null, others: [], quiet: false });

export function ticketState({ detail, liveEvents, series }: TicketStateInput): TicketStateView | null {
  const head = detail.attempts.at(-1) ?? null;
  if (!head) return null;
  const { summary } = detail;
  switch (summary.bucket) {
    case "needs_you": return needsYou(detail, head);
    case "working": return working(detail, head, liveEvents, series);
    case "waiting_on_review": return review(detail, head);
    case "failed": return failed(detail, head, "Failed");
    case "stuck": return failed(detail, head, "Stuck");
    case "pr_closed":
      return { ...base(head), tone: "bad", pill: "PR closed", sentence: "Every pull request was closed without a merge.", act: CONTINUE };
    case "done": return { ...base(head), quiet: true, tone: "ok", pill: "Done", sentence: "Done — the pull requests are merged." };
    case "closed": return { ...base(head), quiet: true, tone: "q", pill: "Closed", sentence: capital(closedWords(summary)) };
    case "idle": return { ...base(head), quiet: true, tone: "q", pill: "Ended", sentence: endedSentence(head) };
    default: return null;
  }
}

/** The act that fits why the head attempt failed, first match wins. */
export function failureAct(head: TicketAttempt, project: string): { sentence: string; act: StateAct; others: StateAct[] } {
  const n = head.number;
  if (head.endedOnInfrastructure) {
    return { sentence: `Attempt ${n} ended on infrastructure: the sandbox or host was lost, not the work.`,
      act: { kind: "retry", attempt: head }, others: [CONTINUE] };
  }
  switch (head.endKind) {
    case RUN_END_KINDS.imageUntrusted:
      return { sentence: `Attempt ${n} stopped before any work: a context names a container image outside the trusted registries.`,
        act: { kind: "link", label: "Trust the registry in Settings", href: SANDBOX_SETTINGS_PATH,
          note: "Add the image's registry to the sandbox's trusted registries, then start the next attempt." },
        others: [CONTINUE] };
    case RUN_END_KINDS.bootstrapMissing:
      return { sentence: `Attempt ${n} stopped before any work: a repository has no agent-smith bootstrap.`,
        act: { kind: "init", project }, others: [CONTINUE] };
    case RUN_END_KINDS.criteriaUnmet:
      return { sentence: `Attempt ${n} failed: the acceptance gate did not accept a criterion.`,
        act: { kind: "link", label: "Overrule in the run", href: runPath(head.runId),
          note: "Records your verdict on the criterion; the gate is not re-run." },
        others: [DISCUSS] };
    case RUN_END_KINDS.preflight:
      return { sentence: `Attempt ${n} stopped before any work: a precondition did not hold.`, act: CONTINUE, others: [] };
    default:
      return { sentence: `Attempt ${n} failed.`, act: CONTINUE, others: [] };
  }
}

function failed(detail: TicketDetail, head: TicketAttempt, pill: string): TicketStateView {
  return { ...base(head), tone: "bad", pill, ...failureAct(head, detail.summary.project) };
}

function needsYou(detail: TicketDetail, head: TicketAttempt): TicketStateView {
  const pending = detail.summary.pendingQuestion;
  if (detail.summary.kind === "question" && pending) {
    const asking = detail.attempts.find((a) => a.runId === pending.runId) ?? head;
    return { ...base(asking), tone: "person", pill: "Needs your answer",
      sentence: `Attempt ${asking.number} is waiting for your answer.`, detail: pending.question.text,
      act: { kind: "tab", label: "Answer in the conversation", tab: "conversation" } };
  }
  return { ...base(head), tone: "person", pill: "Handed back",
    sentence: `Attempt ${head.number} handed the ticket back.`, detail: detail.summary.handback?.text ?? null, act: CONTINUE };
}

function working(detail: TicketDetail, head: TicketAttempt, events: RunEvent[], series: TicketSeries | null): TicketStateView {
  const place = detail.summary.queuePlace;
  const at = place != null ? ` — place ${place} in line` : "";
  if (head.status === "queued") {
    return { ...base(head), quiet: true, tone: "amber", pill: "Queued", sentence: `Attempt ${head.number} waits in the queue${at}.` };
  }
  if (head.status === "waiting_for_input") {
    return { ...base(head), quiet: true, tone: "amber", pill: "Resuming", sentence: `Attempt ${head.number} is resuming${at}.` };
  }
  return { ...base(head), quiet: true, tone: "amber", pill: "Working", sentence: progress(head, series), detail: liveLine(events) };
}

/** "Spec n of m, step k of s: <action>" from the attempt's own cut, else what it is doing before it has one. */
export function progress(head: TicketAttempt, series: TicketSeries | null): string {
  const on = liveSpec(head, series);
  if (!on) return `Attempt ${head.number} is deriving the series.`;
  const action = on.step !== null ? on.spec.steps[on.step - 1]?.action : null;
  const step = on.step !== null ? `, step ${on.step} of ${on.steps}` : "";
  return `Spec ${on.spec.ordinal} of ${on.of}${step}: ${action ?? on.spec.goal}`;
}

/** The newest step the live run started, as its display name says. */
export function liveLine(events: RunEvent[]): string | null {
  const started = events.filter((e): e is StepStartedEvent => e.type === EventType.StepStarted).at(-1);
  return started ? `Now: ${started.displayName ?? started.stepName}` : null;
}

function review(detail: TicketDetail, head: TicketAttempt): TicketStateView {
  const open = (detail.summary.pullRequests ?? []).filter((pr) => pr.state === "open");
  const links: StateAct[] = open.map((pr) => ({
    kind: "link", label: `Open pull request #${prNumber(pr.url) ?? pr.number} (${pr.repo})`, href: pr.url,
  }));
  const count = open.length === 1 ? "1 pull request is" : `${open.length} pull requests are`;
  return { ...base(head), tone: "person", pill: "In review", sentence: `Waiting on review: ${count} open.`,
    act: links[0] ?? null, others: links.slice(1) };
}

function endedSentence(head: TicketAttempt): string {
  if (head.status === "cancelled") return `Attempt ${head.number} was cancelled.`;
  if (head.status === "success" || head.status === "shortfall") return `Attempt ${head.number} finished without a pull request.`;
  return `Attempt ${head.number} ended.`;
}

function capital(words: string): string {
  return words.charAt(0).toUpperCase() + words.slice(1);
}
