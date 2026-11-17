import { describe, expect, it } from "vitest";
import { attempt, defaultEntry, defaults, detail, series, spec } from "./detailFixtures";
import { sectionIdOf, sectionOf, ticketSections, type TicketSectionReads } from "../ticketPageSections";
import { addressQuery } from "../useTicketTabGo";

// 2026-10-13-8762q: the Defaults row — last; its counter counts pending defaults and is drawn in the waits-for-a-person
// colour only while one of them holds the series. 2026-11-17-8d1ca: the rows are the section bar's sections, and every
// older ?tab= lands on the section that holds what that tab held.

const section = (id: string) => ticketSections.find((t) => t.id === id)!;
const reads = (over: Partial<TicketSectionReads>): TicketSectionReads => ({ series: null, defaults: null, ...over });

describe("ticketSections", () => {
  it("ticketSections_AreWhatHappenedSpecsAcceptanceRunsCostDefaults_InThatOrder", () => {
    expect(ticketSections.map((s) => s.label)).toEqual(["What happened", "Specs", "Acceptance", "Runs", "Cost", "Defaults"]);
    expect(section("happened").Pane).toBeNull();
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

  it("sectionOf_SectionWinsOverTab_EveryOlderTabLandsOnItsSection_UnknownIsWhatHappened", () => {
    expect(sectionOf("cost", "specs").id).toBe("cost");
    expect(sectionOf(null, "summary").id).toBe("happened");
    expect(sectionOf(null, "conversation").id).toBe("happened");
    expect(sectionOf(null, "attempts").id).toBe("runs");
    expect(sectionOf(null, "defaults").id).toBe("defaults");
    expect(sectionOf(null, null).id).toBe("happened");
    expect(sectionIdOf("nonsense")).toBe("happened");
  });
});

describe("addressQuery", () => {
  it("addressQuery_WritesTheSectionFirst_DropsTheTab_AndLeavesWhatHappenedOut", () => {
    expect(addressQuery("tab=specs&spec=s1", "specs", { spec: "s2" })).toBe("section=specs&spec=s2");
    expect(addressQuery("section=runs&causeOf=r-1", "conversation", { causeOf: "r-3" })).toBe("causeOf=r-3");
    expect(addressQuery("section=runs", "attempts")).toBe("section=runs");
    expect(addressQuery("tab=cost&causeOf=r-1", null, { causeOf: null })).toBe("");
  });
});
