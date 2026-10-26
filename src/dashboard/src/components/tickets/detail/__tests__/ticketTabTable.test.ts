import { describe, expect, it } from "vitest";
import { attempt, defaultEntry, defaults, detail } from "./detailFixtures";
import { ticketTabs, type TicketTabReads } from "../ticketTabTable";

// 2026-10-13-8762q: the Defaults row — appended last; its counter counts pending defaults and is drawn in the
// waits-for-a-person colour only while one of them holds the series.

const tab = () => ticketTabs.find((t) => t.id === "defaults")!;
const reads = (over: Partial<TicketTabReads>): TicketTabReads => ({ conversation: null, series: null, defaults: null, ...over });

describe("ticketTabs", () => {
  it("ticketTabs_DefaultsRow_IsLast", () => {
    expect(ticketTabs[ticketTabs.length - 1]).toMatchObject({ id: "defaults", label: "Defaults" });
  });

  it("ticketTabs_DefaultsCounter_CountsPending_HotOnlyWhenOneHoldsTheSeries", () => {
    const quiet = defaults([defaultEntry({ id: 1 }), defaultEntry({ id: 2 }), defaultEntry({ id: 3, verdict: "ratified" })]);
    expect(tab().counter!(detail([attempt()]), reads({ defaults: quiet }))).toEqual({ text: "2", hot: false });
    const held = defaults([defaultEntry({ id: 1, holdsSeries: true }), defaultEntry({ id: 2 })]);
    expect(tab().counter!(detail([attempt()]), reads({ defaults: held }))).toEqual({ text: "2", hot: true });
    const decidedHeld = defaults([defaultEntry({ id: 1, holdsSeries: true, verdict: "ratified" })]);
    expect(tab().counter!(detail([attempt()]), reads({ defaults: decidedHeld }))).toEqual({ text: "0", hot: false });
  });

  it("ticketTabs_CostCounter_IncludesSpecDerivations", () => {
    const cost = ticketTabs.find((t) => t.id === "cost")!;
    const derivation = {
      runId: "d-1", status: "success", startedAt: "2026-10-26T09:00:00Z", finishedAt: null, cost: 0.5,
      outcome: "derived" as const, specCount: 1, pullRequests: [], summary: null,
    };
    expect(cost.counter!(detail([attempt({ cost: 2 })], { derivations: [derivation] }), reads({}))).toEqual({ text: "$2.50" });
    expect(cost.counter!(detail([attempt({ cost: 2 })], { derivations: null }), reads({}))).toEqual({ text: "$2.00" });
  });

  it("ticketTabs_NoDefaults_NoCounter", () => {
    expect(tab().counter!(detail([attempt()]), reads({ defaults: defaults() }))).toBeNull();
    expect(tab().counter!(detail([attempt()]), reads({ defaults: null }))).toBeNull();
  });
});
