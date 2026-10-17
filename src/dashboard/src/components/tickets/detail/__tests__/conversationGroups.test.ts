import { describe, expect, it } from "vitest";
import type { TicketConversation } from "@/types/tickets";
import { conversationGroups, currentAttempt, entryCount, sameCauses } from "../conversationGroups";
import { attempt, conversation, detail, entry } from "./detailFixtures";

// 2026-10-17-e730c: which groups of the thread are open by default, from recorded fields only.

const marker = (runId: string, number: number) => ({ runId, number, cause: null, revision: null });

function thread(over: Partial<TicketConversation> = {}): TicketConversation {
  return {
    ...conversation([
      entry({ id: "b0", group: "before", attemptRunId: null }),
      entry({ id: "q1", attemptRunId: "r-1" }),
      entry({ id: "t1", source: "ticket", kind: "comment", role: "person", attemptRunId: "r-1" }),
      entry({ id: "q2", attemptRunId: "r-2" }),
      entry({ id: "q3", attemptRunId: "r-3" }),
      entry({ id: "n", source: "conversation", kind: "turn", role: "person", group: "now", attemptRunId: null }),
    ]),
    attempts: [marker("r-1", 1), marker("r-2", 2), marker("r-3", 3)],
    ...over,
  };
}

const three = () => detail([
  attempt({ status: "failed", summary: "Preflight failed: context-image-trust\nmore" }),
  attempt({ number: 2, runId: "r-2", status: "failed" }),
  attempt({ number: 3, runId: "r-3", status: "failed" }),
]);

const opened = (groups: ReturnType<typeof conversationGroups>) => groups.filter((g) => g.openByDefault).map((g) => g.key);

describe("conversationGroups", () => {
  it("conversationGroups_All_OpensOnlyTheCurrentAttemptAndNow", () => {
    const groups = conversationGroups(thread(), three(), null, null);
    expect(groups.map((g) => g.key)).toEqual(["before", "r-1", "r-2", "r-3", "now"]);
    expect(opened(groups)).toEqual(["r-3", "now"]);
    const first = groups.find((g) => g.key === "r-1")!;
    expect(first.cause).toBe("Preflight failed: context-image-trust");
    expect(first.pill).toEqual(["bad", "failed"]);
    expect(first.entries.map((e) => e.id)).toEqual(["q1", "t1"]);
    expect(groups.find((g) => g.key === "now")!.collapsible).toBe(false);
  });

  it("conversationGroups_LiveAttempt_IsCurrentOverTheNewest_OnlyOnceItHasAGroup", () => {
    const live = detail([
      attempt({ status: "running", finishedAt: null }),
      attempt({ number: 2, runId: "r-2", status: "failed" }),
    ]);
    const two = thread({ attempts: [marker("r-1", 1), marker("r-2", 2)] });
    expect(currentAttempt(two, live)).toBe("r-1");

    const runningFourth = detail([...three().attempts, attempt({ number: 4, runId: "r-4", status: "running", finishedAt: null })]);
    expect(currentAttempt(thread(), runningFourth)).toBe("r-3");
  });

  it("conversationGroups_OnlyBefore_IsOpen", () => {
    const none = { ...conversation([entry({ id: "b0", group: "before", attemptRunId: null })]), attempts: [] };
    expect(opened(conversationGroups(none, detail([]), null, null))).toEqual(["before"]);
    expect(opened(conversationGroups(thread(), three(), null, null))).not.toContain("before");
  });

  it("conversationGroups_PendingQuestionOrCauseTarget_OpensItsGroup", () => {
    const question = { questionId: "q", type: "FreeText", text: "Keep it?", context: null, choices: [], defaultAnswer: null, askedAt: "2026-09-04T14:10:00Z", answerDeadlineAt: "2026-09-05T14:10:00Z" };
    const asking = detail([
      attempt({ status: "failed" }),
      attempt({ number: 2, runId: "r-2", status: "failed", pendingQuestion: question }),
      attempt({ number: 3, runId: "r-3", status: "failed" }),
    ]);
    expect(opened(conversationGroups(thread(), asking, null, null))).toEqual(["r-2", "r-3", "now"]);
    expect(opened(conversationGroups(thread(), three(), null, "t1"))).toEqual(["r-1", "r-3", "now"]);
    expect(opened(conversationGroups(thread(), three(), null, "b0"))).toEqual(["before", "r-3", "now"]);
  });

  it("conversationGroups_NarrowingFilter_OpensEveryGroupWithAMatch", () => {
    const groups = conversationGroups(thread(), three(), "ticket", null);
    expect(groups.map((g) => g.key)).toEqual(["r-1", "r-2", "r-3"]);
    expect(opened(groups)).toEqual(["r-1"]);
    expect(groups.find((g) => g.key === "r-2")!.entries).toEqual([]);
  });

  it("entryCount_SaysOneManyAndNone", () => {
    expect([entryCount(0), entryCount(1), entryCount(3)]).toEqual(["no entries", "1 entry", "3 entries"]);
  });

  it("conversationGroups_SameKindAndSummaryAsThePrevious_IsSameAsTheFirstOfTheRun", () => {
    const cause = { status: "failed", summary: "Preflight failed: context-image-trust", endKind: "image-untrusted" };
    const attempts = [
      attempt({ ...cause }),
      attempt({ number: 2, runId: "r-2", ...cause }),
      attempt({ number: 3, runId: "r-3", ...cause, status: "error" }),
      attempt({ number: 4, runId: "r-4", status: "failed", summary: "Pipeline aborted: missing bootstrap in repos", endKind: "bootstrap-missing" }),
      attempt({ number: 5, runId: "r-5", status: "failed", summary: "Pipeline aborted: missing bootstrap in repos", endKind: "bootstrap-missing" }),
    ];
    const same = sameCauses(attempts);
    expect([...same.entries()]).toEqual([
      ["r-2", { number: 1, key: "r-1" }],
      ["r-3", { number: 1, key: "r-1" }],
      ["r-5", { number: 4, key: "r-4" }],
    ]);
    const groups = conversationGroups(thread(), detail(attempts.slice(0, 3)), null, null);
    expect(groups.map((g) => g.sameAs?.number ?? null)).toEqual([null, null, 1, 1, null]);
  });

  it("conversationGroups_DifferentSummaryOrKind_OrNotFailed_IsNotSameAs", () => {
    const one = attempt({ status: "failed", summary: "Preflight failed: a", endKind: "preflight" });
    const unclassified = sameCauses([
      attempt({ status: "failed", summary: "Pod did not start" }),
      attempt({ number: 2, runId: "r-2", status: "failed", summary: "Pod did not start", endKind: null }),
    ]);
    expect(unclassified.get("r-2")).toEqual({ number: 1, key: "r-1" });
    for (const second of [
      { status: "failed", summary: "Preflight failed: a ", endKind: "preflight" },
      { status: "failed", summary: "Preflight failed: a", endKind: "image-untrusted" },
      { status: "failed", summary: "Preflight failed: a" },
      { status: "cancelled", summary: "Preflight failed: a", endKind: "preflight" },
      { status: "failed", summary: null, endKind: "preflight" },
    ]) {
      expect(sameCauses([one, attempt({ number: 2, runId: "r-2", ...second })]).size).toBe(0);
    }
    expect(sameCauses([attempt({ status: "failed", summary: null }), attempt({ number: 2, runId: "r-2", status: "failed", summary: null })]).size).toBe(0);
  });
});
