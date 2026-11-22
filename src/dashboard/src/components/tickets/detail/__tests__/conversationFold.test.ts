import { describe, expect, it } from "vitest";
import { attempt, detail, entry } from "./detailFixtures";
import { countItems, earlierWords, foldBefore, foldBoundary, itemKey, latestEntry, wholeConversationWords } from "../conversationFold";
import { nowRun } from "../nowRun";
import type { ThreadItem } from "../threadItems";

// 2026-11-22-4321a: the Conversation section folds all but its newest items; Now shows the newest entry and the
// current run.

const item = (id: string): ThreadItem => ({ kind: "entry", entry: entry({ id }) });
const ids = (items: ThreadItem[]) => items.map((i) => (i.kind === "entry" ? i.entry.id : i.kind));
const question = { questionId: "q", type: "FreeText", text: "Keep it?", context: null, choices: [], defaultAnswer: null,
  askedAt: "2026-09-04T14:10:00Z", answerDeadlineAt: null };
const derivation = (runId: string, startedAt: string, outcome: "running" | "derived") => ({
  runId, status: outcome === "running" ? "running" : "success", startedAt, finishedAt: null, cost: 0.1,
  outcome, specCount: 1, pullRequests: [], summary: null,
});

describe("conversationFold", () => {
  it("foldBoundary_IsTheFirstOfTheNewestN_NullWhenTheyFit", () => {
    const items = ["a", "b", "c", "d"].map(item);
    expect(foldBoundary(items, 2)).toBe("entry-c");
    expect(foldBoundary(items, 4)).toBeNull();
    expect(itemKey({ kind: "report", attempt: attempt({ runId: "r-9" }) })).toBe("report-r-9");
  });

  it("foldBefore_FoldsWhatPrecedesTheBoundary_AnArrivalAppendsToTheShown", () => {
    const items = ["a", "b", "c", "d"].map(item);
    expect(ids(foldBefore(items, "entry-c").earlier)).toEqual(["a", "b"]);
    expect(ids(foldBefore([...items, item("e")], "entry-c").shown)).toEqual(["c", "d", "e"]);
    expect(foldBefore(items, null).earlier).toEqual([]);
    expect(foldBefore(items, "entry-gone").earlier).toEqual([]);
  });

  it("countItems_CountsEntriesAndRuns_IntoARepeat", () => {
    const report: ThreadItem = { kind: "report", attempt: attempt() };
    const repeat: ThreadItem = { kind: "repeat", key: "k", line: "", items: [item("x"), report, report] };
    expect(countItems([item("a"), report, repeat])).toEqual({ entries: 2, runs: 3 });
  });

  it("latestEntry_IsTheLastEntry_LookingIntoARepeat_NullWithoutAny", () => {
    const report: ThreadItem = { kind: "report", attempt: attempt() };
    expect(latestEntry([item("a"), item("b"), report])?.id).toBe("b");
    expect(latestEntry([item("a"), { kind: "repeat", key: "k", line: "", items: [item("x"), report] }])?.id).toBe("x");
    expect(latestEntry([report])).toBeNull();
  });

  it("earlierWords_AndWholeConversationWords_CountInWords", () => {
    expect(earlierWords({ entries: 1, runs: 0 })).toBe("1 earlier entry");
    expect(earlierWords({ entries: 4, runs: 1 })).toBe("4 earlier entries and 1 run");
    expect(earlierWords({ entries: 0, runs: 2 })).toBe("2 earlier runs");
    expect(wholeConversationWords(1)).toBe("Whole conversation · 1 entry →");
    expect(wholeConversationWords(12)).toBe("Whole conversation · 12 entries →");
  });
});

describe("nowRun", () => {
  it("nowRun_IsTheAskingAttempt_ElseTheNewest", () => {
    const asking = attempt({ number: 1, runId: "r-1", status: "waiting_for_input", pendingQuestion: question });
    expect(nowRun(detail([asking, attempt({ number: 2, runId: "r-2" })])).attempt?.runId).toBe("r-1");
    expect(nowRun(detail([attempt(), attempt({ number: 2, runId: "r-2" })])).attempt?.runId).toBe("r-2");
    expect(nowRun(detail([])).attempt).toBeNull();
  });

  it("nowRun_ARunningDerivationBesideTheAttempt_TheNewestDerivationWithoutAnAttempt", () => {
    const running = derivation("d-2", "2026-09-04T15:00:00Z", "running");
    const done = derivation("d-1", "2026-09-04T10:00:00Z", "derived");
    expect(nowRun(detail([attempt()], { derivations: [done, running] })).derivation?.runId).toBe("d-2");
    expect(nowRun(detail([attempt()], { derivations: [done] })).derivation).toBeNull();
    expect(nowRun(detail([], { derivations: [done] })).derivation?.runId).toBe("d-1");
  });
});
