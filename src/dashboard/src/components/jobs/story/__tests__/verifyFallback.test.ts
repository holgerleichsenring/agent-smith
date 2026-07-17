import { describe, it, expect } from "vitest";
import {
  buildVerifyFallback,
  findRatifiedEvent,
  parseExpectationJson,
} from "../verifyFallback";
import { EventType, type RunEvent } from "@/types/hub-events";

// p0344b: the LEGACY event-derived verify view — kept only for runs persisted
// before snapshot.acceptance existed. Same honesty rules: green only for a
// genuinely ratified contract.

function ratifiedEvent(over: Partial<{ outcome: string; ratifiedJson: string; ratifiedBy: string; editDistance: number }> = {}): RunEvent {
  return {
    runId: "r1",
    type: EventType.ExpectationRatified,
    timestamp: "2026-07-17T10:00:00Z",
    draftJson: "{}",
    ratifiedJson: JSON.stringify({ Observed: "bug", Expected: ["fixed"], Constraints: [] }),
    outcome: "verbatim",
    ratifiedBy: "holger",
    editDistance: 0,
    ...over,
  } as RunEvent;
}

describe("verifyFallback", () => {
  it("FindRatifiedEvent_PicksLatestEvent", () => {
    const first = ratifiedEvent({ outcome: "rejected" });
    const second = ratifiedEvent({ outcome: "verbatim" });
    expect(findRatifiedEvent([first, second])?.outcome).toBe("verbatim");
    expect(findRatifiedEvent([])).toBeNull();
  });

  it("ParseExpectationJson_AcceptsPascalAndCamelCase", () => {
    expect(parseExpectationJson('{"Observed":"o","Expected":["e"],"Constraints":["c"]}')).toEqual({
      observed: "o",
      expected: ["e"],
      constraints: ["c"],
    });
    expect(parseExpectationJson('{"observed":"o","expected":["e"],"constraints":[]}')).toEqual({
      observed: "o",
      expected: ["e"],
      constraints: [],
    });
    expect(parseExpectationJson("not json")).toBeNull();
  });

  it("BuildVerifyFallback_Ratified_GreenTone", () => {
    const view = buildVerifyFallback([ratifiedEvent()]);
    expect(view.ratified).toBe(true);
    expect(view.tone).toBe("green");
    expect(view.expectation?.expected).toEqual(["fixed"]);
  });

  it("BuildVerifyFallback_Rejected_RoseTone_NeverRatified", () => {
    const view = buildVerifyFallback([ratifiedEvent({ outcome: "rejected" })]);
    expect(view.ratified).toBe(false);
    expect(view.tone).toBe("rose");
  });

  it("BuildVerifyFallback_NoEvent_HonestNone", () => {
    const view = buildVerifyFallback([]);
    expect(view.outcome).toBe("none");
    expect(view.tone).toBe("neutral");
    expect(view.ratified).toBe(false);
    expect(view.expectation).toBeNull();
  });
});
