import { describe, expect, it } from "vitest";
import { attempt, conversation, defaultEntry, defaults, detail, entry, series, spec } from "./detailFixtures";
import { sectionIdOf, sectionOf, ticketSections, type TicketSectionReads } from "../ticketPageSections";
import { addressQuery } from "../useTicketTabGo";

// 2026-10-13-8762q: the Defaults row — last; its counter counts pending defaults and is drawn in the waits-for-a-person
// colour only while one of them holds the series. 2026-11-17-8d1ca: the rows are the section bar's sections, and every
// older ?tab= lands on the section that holds what that tab held.

const section = (id: string) => ticketSections.find((t) => t.id === id)!;
const reads = (over: Partial<TicketSectionReads>): TicketSectionReads => ({ series: null, defaults: null, conversation: null, ...over });

describe("ticketSections", () => {
  // 2026-11-22-4321a: Now takes What happened's place; the conversation is the section after it.
  it("ticketSections_AreNowConversationSpecsAcceptanceRunsCostDefaults_InThatOrder", () => {
    expect(ticketSections.map((s) => s.label)).toEqual(["Now", "Conversation", "Specs", "Acceptance", "Runs", "Cost", "Defaults"]);
    expect(section("now").Pane).toBeNull();
    expect(section("conversation").Pane).toBeNull();
  });

  it("ticketSections_ConversationCounter_CountsEntries_NothingBeforeTheRead", () => {
    const read = conversation([entry({ id: "a" }), entry({ id: "b" }), entry({ id: "c" })]);
    expect(section("conversation").counter!(detail([attempt()]), reads({ conversation: read }))).toEqual({ text: "3" });
    expect(section("conversation").counter!(detail([attempt()]), reads({}))).toBeNull();
  });

  it("ticketSections_DefaultsCounter_CountsPending_HotOnlyWhenOneHoldsTheSeries", () => {
    const quiet = defaults([defaultEntry({ id: 1 }), defaultEntry({ id: 2 }), defaultEntry({ id: 3, verdict: "ratified" })]);
    expect(section("defaults").counter!(detail([attempt()]), reads({ defaults: quiet }))).toEqual({ text: "2", hot: false });
    const held = defaults([defaultEntry({ id: 1, holdsSeries: true }), defaultEntry({ id: 2 })]);
    expect(section("defaults").counter!(detail([attempt()]), reads({ defaults: held }))).toEqual({ text: "2", hot: true });
    expect(section("defaults").counter!(detail([attempt()]), reads({ defaults: defaults() }))).toBeNull();
  });

  it("ticketSections_CostCounter_IncludesSpecDerivations", () => {
    const derivation = {
      runId: "d-1", status: "success", startedAt: "2026-10-26T09:00:00Z", finishedAt: null, cost: 0.5,
      outcome: "derived" as const, specCount: 1, pullRequests: [], summary: null,
    };
    expect(section("cost").counter!(detail([attempt({ cost: 2 })], { derivations: [derivation] }), reads({}))).toEqual({ text: "$2.50" });
    expect(section("cost").counter!(detail([attempt({ cost: 2 })], { derivations: null }), reads({}))).toEqual({ text: "$2.00" });
  });

  it("ticketSections_RunsCountsAttempts_AcceptanceIsOkOnlyWhenEveryCriterionIsVerified", () => {
    expect(section("runs").counter!(detail([attempt(), attempt({ number: 2, runId: "r-2" })]), reads({}))).toEqual({ text: "2" });
    const met = series("r-1", [spec()]);
    expect(section("acceptance").counter!(detail([attempt()]), reads({ series: met }))).toEqual({ text: "1/1", ok: true });
  });

  it("sectionOf_SectionWinsOverTab_EveryOlderNameLandsOnItsSection_UnknownIsNow", () => {
    expect(sectionOf("cost", "specs").id).toBe("cost");
    expect(sectionOf(null, "summary").id).toBe("now");
    expect(sectionOf("happened", null).id).toBe("now");
    expect(sectionOf(null, "conversation").id).toBe("conversation");
    expect(sectionOf(null, "attempts").id).toBe("runs");
    expect(sectionOf(null, "defaults").id).toBe("defaults");
    expect(sectionOf(null, null).id).toBe("now");
    expect(sectionIdOf("nonsense")).toBe("now");
  });

  // 2026-11-22-4321a: an older "read" link carried only ?causeOf=; its entry is in the Conversation section now.
  it("sectionOf_ABareCauseOf_LandsOnTheConversation_ANamedSectionWins", () => {
    expect(sectionOf(null, null, "r-1").id).toBe("conversation");
    expect(sectionOf("runs", null, "r-1").id).toBe("runs");
    expect(sectionOf(null, "summary", "r-1").id).toBe("now");
  });
});

describe("addressQuery", () => {
  it("addressQuery_WritesTheSectionFirst_DropsTheTab_AndLeavesNowOut", () => {
    expect(addressQuery("tab=specs&spec=s1", "specs", { spec: "s2" })).toBe("section=specs&spec=s2");
    expect(addressQuery("section=runs&causeOf=r-1", "conversation", { causeOf: "r-3" })).toBe("section=conversation&causeOf=r-3");
    expect(addressQuery("section=runs", "happened")).toBe("");
    expect(addressQuery("section=conversation", "now")).toBe("");
    expect(addressQuery("section=runs", "attempts")).toBe("section=runs");
    expect(addressQuery("tab=cost&causeOf=r-1", null, { causeOf: null })).toBe("");
  });
});
