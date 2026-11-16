import { summaryLine } from "@/lib/formatRunSummary";
import type { TicketAttempt, TicketConversation, TicketConversationEntry, TicketDerivation, TicketDetail } from "@/types/tickets";
import { sameCauses } from "./sameCauses";

// 2026-11-16-6877a: the ticket thread in order. What came before the first attempt; then each attempt and each spec
// derivation by when it started, an attempt's entries by time with its own report placed where it ended (last while
// it runs); then the ticket conversation of now. Repeats fold: a run of failed attempts with the same end kind and
// end text, none carrying an entry of its own, becomes one line with a count, and so do derivations in a row that
// came to the same words. Every choice reads a recorded field.

export type ThreadItem =
  | { kind: "entry"; entry: TicketConversationEntry }
  | { kind: "report"; attempt: TicketAttempt }
  | { kind: "derivation"; derivation: TicketDerivation }
  | { kind: "repeat"; key: string; line: string; items: ThreadItem[] };

interface Block {
  at: number;
  /** The attempt or derivation the block is about, for folding repeats. */
  attempt?: TicketAttempt;
  derivation?: TicketDerivation;
  items: ThreadItem[];
  /** Whether the block holds entries beside its report: such a block never folds. */
  hasEntries: boolean;
}

export function threadItems(conversation: TicketConversation | null, detail: TicketDetail): ThreadItem[] {
  const entries = conversation?.entries ?? [];
  const attemptIds = new Set(detail.attempts.map((a) => a.runId));
  const before = entries.filter((e) => !e.attemptRunId && e.group === "before");
  const now = entries.filter((e) => (!e.attemptRunId && e.group !== "before") || (e.attemptRunId && !attemptIds.has(e.attemptRunId)));
  const blocks: Block[] = [
    ...[...detail.attempts].sort((l, r) => l.number - r.number).map((a) => attemptBlock(a, entries.filter((e) => e.attemptRunId === a.runId))),
    ...(detail.derivations ?? []).map(derivationBlock),
  ].sort((l, r) => l.at - r.at);
  return [
    ...before.map(asEntry),
    ...foldDerivations(foldAttempts(blocks, detail.attempts)).flatMap((b) => b.items),
    ...now.map(asEntry),
  ];
}

function asEntry(entry: TicketConversationEntry): ThreadItem {
  return { kind: "entry", entry };
}

function time(iso: string | null | undefined): number {
  const at = iso ? Date.parse(iso) : NaN;
  return Number.isNaN(at) ? Number.MAX_SAFE_INTEGER : at;
}

function attemptBlock(attempt: TicketAttempt, own: TicketConversationEntry[]): Block {
  const ended = attempt.finishedAt ? time(attempt.finishedAt) : Number.MAX_SAFE_INTEGER;
  const sorted = [...own].sort((l, r) => time(l.at) - time(r.at));
  const until = sorted.filter((e) => time(e.at) <= ended);
  const after = sorted.filter((e) => time(e.at) > ended);
  return {
    at: time(attempt.startedAt), attempt, hasEntries: own.length > 0,
    items: [...until.map(asEntry), { kind: "report", attempt }, ...after.map(asEntry)],
  };
}

function derivationBlock(derivation: TicketDerivation): Block {
  return { at: time(derivation.startedAt), derivation, hasEntries: false, items: [{ kind: "derivation", derivation }] };
}

/** Consecutive blocks of attempts in one run of equal causes fold into one line once there are two or more. */
function foldAttempts(blocks: Block[], attempts: TicketAttempt[]): Block[] {
  const runs = sameCauses(attempts);
  const headOf = (b: Block) => (b.attempt ? runs.get(b.attempt.runId)?.key ?? b.attempt.runId : null);
  return fold(blocks, (prev, next) =>
    !!prev.attempt && !!next.attempt && !prev.hasEntries && !next.hasEntries
    && runs.has(next.attempt.runId) && headOf(next) === headOf(prev),
  (run) => {
    const cause = summaryLine(run[0].attempt!.summary) ?? "the same cause";
    return { key: `attempts-${run[0].attempt!.runId}`, line: `${run.length} runs stopped: ${cause}` };
  });
}

/** Consecutive derivations that came to the same words fold into one line once there are two or more. */
function foldDerivations(blocks: Block[]): Block[] {
  return fold(blocks, (prev, next) =>
    !!prev.derivation && !!next.derivation && next.derivation.outcome !== "running"
    && derivationWords(prev.derivation) === derivationWords(next.derivation),
  (run) => ({ key: `derivations-${run[0].derivation!.runId}`, line: `${run.length} derivations ${derivationWords(run[0].derivation!)}` }));
}

function fold(blocks: Block[], joins: (prev: Block, next: Block) => boolean, name: (run: Block[]) => { key: string; line: string }): Block[] {
  const out: Block[] = [];
  let run: Block[] = [];
  const flush = () => {
    if (run.length >= 2) {
      const { key, line } = name(run);
      out.push({ at: run[0].at, hasEntries: false, items: [{ kind: "repeat", key, line, items: run.flatMap((b) => b.items) }] });
    } else out.push(...run);
    run = [];
  };
  for (const block of blocks) {
    if (run.length > 0 && !joins(run[run.length - 1], block)) flush();
    run.push(block);
  }
  flush();
  return out;
}

/** What came of a derivation, after "Spec derivation" or "3 derivations". */
export function derivationWords(d: TicketDerivation): string {
  switch (d.outcome) {
    case "running": return "is running";
    case "derived": return d.specCount === 1 ? "found 1 spec" : `found ${d.specCount} specs`;
    case "asked": return "stopped on a question, asked on the ticket";
    default: return `stopped: ${summaryLine(d.summary) ?? "no reason was recorded"}`;
  }
}
