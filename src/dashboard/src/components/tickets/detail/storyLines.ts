import { attemptStatus, statusRest, type RunStatusLine } from "@/components/jobs/runlog/runStatusLine";
import { translator, type T } from "@/lib/i18n/translate";
import type { TicketAttempt, TicketDerivation } from "@/types/tickets";
import { money } from "../ticketFormat";
import type { StatusTone as OutcomeTone } from "@/components/jobs/runlog/runStatusLine";
import { elapsed } from "./flowWords";
import { derivationWords, type ThreadItem } from "./threadItems";

// 2026-11-17-8d1cb: What happened as lines (TicketRail mock) — each run, derivation and folded repeat in one line:
// when it ended, who, how it ended in words, one sentence, how long it took. Entries carry their own words
// (ConversationEntry); these are the lines the page draws for what the runs did. Every word reads a recorded field.

export interface StoryLine {
  /** "06:11" or "Nov 13 06:11", the end, or the start while it runs. */
  at: string | null;
  who: string;
  /** The outcome in words, drawn in the line's colour: "failed · image not trusted". */
  outcome: string | null;
  /** One sentence: the run's own first line, or its state. */
  text: string;
  /** Right-aligned: "3m 40s", "4× same cause". */
  meta: string;
  tone: OutcomeTone;
}

/** One attempt as a line. 2026-11-22-7103a: its outcome is the run's status head and its text the status line's
 *  clauses and next step (runStatusLine), so the row is the run's one status sentence. */
export function runStoryLine(
  attempt: TicketAttempt, status: RunStatusLine = attemptStatus(attempt, null), t: T = translator("en"),
): StoryLine {
  return {
    at: attempt.finishedAt ?? attempt.startedAt,
    who: t("tickets.story.run", { n: attempt.number }),
    outcome: status.head,
    text: statusRest(status),
    meta: elapsed(attempt.startedAt, attempt.finishedAt, t),
    tone: status.tone,
  };
}

/** One spec derivation as a line: what came of it, how long and what it cost. */
export function derivationStoryLine(derivation: TicketDerivation, t: T = translator("en")): StoryLine {
  const running = derivation.outcome === "running";
  const cost = running ? t("tickets.story.soFar", { cost: money(derivation.cost) }) : money(derivation.cost);
  return {
    at: derivation.finishedAt ?? derivation.startedAt,
    who: t("tickets.story.derivation"),
    outcome: null,
    text: t("tickets.story.derivationText", { words: derivationWords(t, derivation) }),
    meta: [elapsed(derivation.startedAt, derivation.finishedAt, t), cost].filter(Boolean).join(" · "),
    tone: running ? "run" : derivation.outcome === "not-finished" ? "bad" : derivation.outcome === "asked" ? "person" : "ok",
  };
}

/** A folded run of equal repeats: "Runs 3–6 · 4× same cause", or "Derivations · 3×". 2026-11-22-7103a: the line is the
 *  LAST run's status line (`status`, with its next step when the page passes one). */
export function foldStoryLine(items: ThreadItem[], status?: RunStatusLine, t: T = translator("en")): StoryLine {
  const reports = items.flatMap((i) => (i.kind === "report" ? [i.attempt] : []));
  const derivations = items.flatMap((i) => (i.kind === "derivation" ? [i.derivation] : []));
  if (reports.length > 0) {
    const first = reports[0];
    const last = reports[reports.length - 1];
    const line = runStoryLine(last, status, t);
    return { ...line, at: last.finishedAt ?? last.startedAt, who: t("tickets.story.runs", { first: first.number, last: last.number }),
      meta: t("tickets.story.sameCause", { count: reports.length }) };
  }
  const line = derivationStoryLine(derivations[0], t);
  return { ...line, at: derivations[derivations.length - 1].finishedAt ?? line.at, who: t("tickets.story.derivations"),
    text: t("tickets.story.derivationsText", { count: derivations.length, words: derivationWords(t, derivations[0]) }),
    meta: `${derivations.length}×` };
}

/** The ticket's first sentence as plain text, for its line: tags and markdown marks dropped, cut near 160 characters. */
export function ticketLead(description: string | null | undefined, t: T = translator("en")): string {
  const plain = (description ?? "")
    .replace(/<[^>]*>/g, " ")
    .replace(/&nbsp;/g, " ").replace(/&amp;/g, "&").replace(/&lt;/g, "<").replace(/&gt;/g, ">")
    .replace(/[#*_`>]+/g, " ")
    .replace(/\s+/g, " ")
    .trim();
  if (!plain) return t("tickets.story.noDescription");
  const sentence = /^.*?[.!?](\s|$)/.exec(plain)?.[0].trim() ?? plain;
  return sentence.length <= 160 ? sentence : `${sentence.slice(0, 159).trimEnd()}…`;
}
