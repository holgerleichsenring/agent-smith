import { summaryLine } from "@/lib/formatRunSummary";
import type { TicketAttempt, TicketDerivation } from "@/types/tickets";
import { money } from "../ticketFormat";
import { attemptWords, outcomeTone, type OutcomeTone } from "./attemptOutcome";
import { elapsed } from "./flowWords";
import { reportText } from "./reportWords";
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

/** One attempt as a line. */
export function runStoryLine(attempt: TicketAttempt): StoryLine {
  const live = attempt.status === "running" || attempt.status === "queued" || attempt.status === "waiting_for_input";
  return {
    at: attempt.finishedAt ?? attempt.startedAt,
    who: `Run ${attempt.number}`,
    outcome: live ? null : attemptWords(attempt),
    text: live ? reportText(attempt) : summaryLine(attempt.summary) ?? reportText(attempt),
    meta: elapsed(attempt.startedAt, attempt.finishedAt),
    tone: outcomeTone(attempt),
  };
}

/** One spec derivation as a line: what came of it, how long and what it cost. */
export function derivationStoryLine(derivation: TicketDerivation): StoryLine {
  const running = derivation.outcome === "running";
  const cost = running ? `${money(derivation.cost)} so far` : money(derivation.cost);
  return {
    at: derivation.finishedAt ?? derivation.startedAt,
    who: "Derivation",
    outcome: null,
    text: `Spec derivation ${derivationWords(derivation)}.`,
    meta: [elapsed(derivation.startedAt, derivation.finishedAt), cost].filter(Boolean).join(" · "),
    tone: running ? "run" : derivation.outcome === "not-finished" ? "bad" : derivation.outcome === "asked" ? "person" : "ok",
  };
}

/** A folded run of equal repeats: "Runs 3–6 · 4× same cause", or "Derivations · 3×". */
export function foldStoryLine(items: ThreadItem[]): StoryLine {
  const reports = items.flatMap((i) => (i.kind === "report" ? [i.attempt] : []));
  const derivations = items.flatMap((i) => (i.kind === "derivation" ? [i.derivation] : []));
  if (reports.length > 0) {
    const first = reports[0];
    const last = reports[reports.length - 1];
    const line = runStoryLine(first);
    return { ...line, at: last.finishedAt ?? last.startedAt, who: `Runs ${first.number}–${last.number}`, meta: `${reports.length}× same cause` };
  }
  const line = derivationStoryLine(derivations[0]);
  return { ...line, at: derivations[derivations.length - 1].finishedAt ?? line.at, who: "Derivations",
    text: `${derivations.length} derivations ${derivationWords(derivations[0])}.`, meta: `${derivations.length}×` };
}

/** The ticket's first sentence as plain text, for its line: tags and markdown marks dropped, cut near 160 characters. */
export function ticketLead(description: string | null | undefined): string {
  const plain = (description ?? "")
    .replace(/<[^>]*>/g, " ")
    .replace(/&nbsp;/g, " ").replace(/&amp;/g, "&").replace(/&lt;/g, "<").replace(/&gt;/g, ">")
    .replace(/[#*_`>]+/g, " ")
    .replace(/\s+/g, " ")
    .trim();
  if (!plain) return "No description.";
  const sentence = /^.*?[.!?](\s|$)/.exec(plain)?.[0].trim() ?? plain;
  return sentence.length <= 160 ? sentence : `${sentence.slice(0, 159).trimEnd()}…`;
}
