import type { TicketConversationEntry } from "@/types/tickets";
import type { ThreadItem } from "./threadItems";

// 2026-11-22-4321a: the Conversation section shows its newest items and folds the earlier ones behind one toggle;
// Now shows only the newest entry. Both read threadItems' order, whose last item is the newest. The fold's boundary
// is the item that was first shown when the section opened, so arrivals append below it and never push a shown
// item into the fold.

/** How many of the thread's items the Conversation section shows before its earlier ones. */
export const NEWEST_SHOWN = 5;

export interface FoldedThread {
  earlier: ThreadItem[];
  shown: ThreadItem[];
}

/** An item's identity across re-reads: the entry id, the run id of a report or derivation, a repeat's key. */
export function itemKey(item: ThreadItem): string {
  switch (item.kind) {
    case "entry": return `entry-${item.entry.id}`;
    case "report": return `report-${item.attempt.runId}`;
    case "derivation": return `derivation-${item.derivation.runId}`;
    case "repeat": return item.key;
  }
}

/** The key of the first of the newest `keep` items, where the fold ends; null when every item fits. */
export function foldBoundary(items: ThreadItem[], keep: number): string | null {
  return items.length > keep ? itemKey(items[items.length - keep]) : null;
}

/** The items before `boundary` folded, the rest shown; nothing folded without a boundary or once it is gone. */
export function foldBefore(items: ThreadItem[], boundary: string | null): FoldedThread {
  const at = boundary === null ? -1 : items.findIndex((item) => itemKey(item) === boundary);
  if (at <= 0) return { earlier: [], shown: items };
  return { earlier: items.slice(0, at), shown: items.slice(at) };
}

export interface ItemCount {
  /** Conversation entries — what the section bar and Now's link count. */
  entries: number;
  /** Attempt reports and derivations. */
  runs: number;
}

/** The entries and runs a list of items holds, a folded repeat's included. */
export function countItems(items: ThreadItem[]): ItemCount {
  return items.reduce<ItemCount>((sum, item) => {
    if (item.kind === "entry") return { ...sum, entries: sum.entries + 1 };
    if (item.kind === "repeat") {
      const inner = countItems(item.items);
      return { entries: sum.entries + inner.entries, runs: sum.runs + inner.runs };
    }
    return { ...sum, runs: sum.runs + 1 };
  }, { entries: 0, runs: 0 });
}

/** The newest conversation entry in thread order, a folded repeat's entries included; null when there is none. */
export function latestEntry(items: ThreadItem[]): TicketConversationEntry | null {
  for (let i = items.length - 1; i >= 0; i--) {
    const item = items[i];
    if (item.kind === "entry") return item.entry;
    if (item.kind === "repeat") {
      const inner = latestEntry(item.items);
      if (inner) return inner;
    }
  }
  return null;
}

/** "3 earlier entries and 2 runs", "1 earlier entry", "2 earlier runs". */
export function earlierWords({ entries, runs }: ItemCount): string {
  const entryWords = entries === 1 ? "1 earlier entry" : `${entries} earlier entries`;
  const runWords = runs === 1 ? "1 run" : `${runs} runs`;
  if (runs === 0) return entryWords;
  if (entries === 0) return runs === 1 ? "1 earlier run" : `${runs} earlier runs`;
  return `${entryWords} and ${runWords}`;
}

/** "Whole conversation · 12 entries →" */
export function wholeConversationWords(entries: number): string {
  return `Whole conversation · ${entries === 1 ? "1 entry" : `${entries} entries`} →`;
}
