import { describe, expect, it } from "vitest";
import type { TicketDerivation } from "@/types/tickets";
import { derivationWords, threadItems, type ThreadItem } from "../threadItems";
import { attempt, conversation, detail, entry } from "./detailFixtures";

// 2026-11-16-6877a: the thread's order and its folds, from recorded fields only.

function derivation(runId: string, startedAt: string, over: Partial<TicketDerivation> = {}): TicketDerivation {
  return { runId, status: "failed", startedAt, finishedAt: startedAt, cost: 0.2, outcome: "not-finished", specCount: 0,
    pullRequests: [], summary: "Pipeline aborted: missing bootstrap", ...over };
}

const names = (items: ThreadItem[]): string[] => items.map((i) => {
  switch (i.kind) {
    case "entry": return i.entry.id;
    case "report": return `report-${i.attempt.number}`;
    case "derivation": return `derivation-${i.derivation.runId}`;
    case "repeat": return `repeat(${names(i.items).join(",")})`;
  }
});

describe("threadItems", () => {
  it("threadItems_Order_BeforeThenAttemptsAndDerivationsByStart_ThenNow", () => {
    const d = detail([
      attempt({ startedAt: "2026-09-04T10:00:00Z", finishedAt: "2026-09-04T10:30:00Z" }),
      attempt({ number: 2, runId: "r-2", startedAt: "2026-09-04T12:00:00Z", finishedAt: null, status: "running" }),
    ], { derivations: [derivation("d-1", "2026-09-04T11:00:00Z", { outcome: "derived", specCount: 2 })] });
    const c = conversation([
      entry({ id: "before", group: "before", attemptRunId: null }),
      entry({ id: "late", attemptRunId: "r-1", at: "2026-09-04T10:45:00Z" }),
      entry({ id: "during", attemptRunId: "r-1", at: "2026-09-04T10:10:00Z" }),
      entry({ id: "live", attemptRunId: "r-2", at: "2026-09-04T12:10:00Z" }),
      entry({ id: "now", group: "now", attemptRunId: null }),
      entry({ id: "orphan", attemptRunId: "r-9" }),
    ]);
    expect(names(threadItems(c, d))).toEqual(["before", "during", "report-1", "late", "derivation-d-1", "live", "report-2", "now", "orphan"]);
  });

  it("threadItems_NoConversationRead_StillListsTheReports", () => {
    expect(names(threadItems(null, detail([attempt()])))).toEqual(["report-1"]);
  });

  it("threadItems_ThreeEqualFailures_FoldIntoOneLineWithACount", () => {
    const cause = { status: "failed", summary: "Preflight failed: context-image-trust\nmore", endKind: "image-untrusted" };
    const d = detail([
      attempt({ ...cause, startedAt: "2026-09-04T10:00:00Z" }),
      attempt({ number: 2, runId: "r-2", ...cause, startedAt: "2026-09-04T11:00:00Z" }),
      attempt({ number: 3, runId: "r-3", ...cause, startedAt: "2026-09-04T12:00:00Z" }),
      attempt({ number: 4, runId: "r-4", startedAt: "2026-09-04T13:00:00Z" }),
    ]);
    const items = threadItems(conversation([]), d);
    expect(names(items)).toEqual(["repeat(report-1,report-2,report-3)", "report-4"]);
    expect(items[0]).toMatchObject({ kind: "repeat", line: "3 runs stopped: Preflight failed: context-image-trust" });
  });

  it("threadItems_OneFailureAlone_DoesNotFold", () => {
    const d = detail([attempt({ status: "failed", summary: "x" }), attempt({ number: 2, runId: "r-2", status: "failed", summary: "y" })]);
    expect(names(threadItems(conversation([]), d))).toEqual(["report-1", "report-2"]);
  });

  it("threadItems_EqualDerivationsInARow_Fold_ADifferentOneBreaksTheRun", () => {
    const d = detail([], { derivations: [
      derivation("d-1", "2026-09-04T10:00:00Z"), derivation("d-2", "2026-09-04T11:00:00Z"), derivation("d-3", "2026-09-04T12:00:00Z"),
      derivation("d-4", "2026-09-04T13:00:00Z", { outcome: "derived", specCount: 1 }),
    ] });
    const items = threadItems(conversation([]), d);
    expect(names(items)).toEqual(["repeat(derivation-d-1,derivation-d-2,derivation-d-3)", "derivation-d-4"]);
    expect(items[0]).toMatchObject({ line: "3 derivations stopped: Pipeline aborted: missing bootstrap" });
  });

  it("derivationWords_SayWhatCameOfIt", () => {
    expect(derivationWords(derivation("d", "t", { outcome: "derived", specCount: 1 }))).toBe("found 1 spec");
    expect(derivationWords(derivation("d", "t", { outcome: "asked" }))).toBe("stopped on a question, asked on the ticket");
    expect(derivationWords(derivation("d", "t", { outcome: "not-finished", summary: null }))).toBe("stopped: no reason was recorded");
    expect(derivationWords(derivation("d", "t", { outcome: "running" }))).toBe("is running");
  });
});
