import { describe, expect, it } from "vitest";
import { translator } from "@/lib/i18n/translate";
import { causeWords, channelWords } from "../ticketFormat";

// 2026-10-13-8762q: an attempt an overrule started reads as one on the Attempts tab; 2026-10-13-8762w: one the
// series started by itself reads as an automatic series start.
// 2026-11-22-50d7a: a list row's starter names the person, "someone" where no one is recorded, never an id.

const en = translator("en");
const de = translator("de");

describe("ticketFormat", () => {
  it("channelWords_Overrule_IsOverruledDefault", () => {
    expect(channelWords("overrule")).toBe("overruled default");
    expect(causeWords(en, { channel: "overrule", actor: "m.reviewer", source: null })).toBe("started by an overruled default by m.reviewer");
  });

  it("ChannelWords_Series_IsAutomaticSeriesStart", () => {
    expect(channelWords("series")).toBe("series, started automatically");
    expect(causeWords(en, { channel: "series", actor: "agent-smith", source: null })).toBe("started by the series");
  });

  // 2026-11-16-f3fbc: an attempt agent-smith started once the precondition its predecessor stopped on held.
  it("ChannelWords_Precondition_IsRestartOncePreconditionHeld", () => {
    expect(channelWords("precondition")).toBe("restarted once the precondition held");
    expect(causeWords(en, { channel: "precondition", actor: null, source: null })).toBe("restarted once the precondition held");
  });

  it("CauseWords_Retry_NamesThePerson", () => {
    expect(causeWords(en, { channel: "retry", actor: "Ada", source: null })).toBe("restarted by Ada");
    expect(causeWords(de, { channel: "retry", actor: "Ada", source: null })).toBe("neu gestartet von Ada");
  });

  it("CauseWords_PersonChannelWithoutActor_IsSomeone", () => {
    expect(causeWords(en, { channel: "retry", actor: null, source: null })).toBe("restarted by someone");
    expect(causeWords(en, { channel: "retry", actor: "", source: null })).toBe("restarted by someone");
    expect(causeWords(de, { channel: "retry", actor: null, source: null })).toBe("neu gestartet von jemandem");
  });

  it("CauseWords_SystemChannelWithoutActor_NamesNoOne", () => {
    expect(causeWords(en, { channel: "requested", actor: null, source: null })).toBe("started on request");
  });

  it("CauseWords_StatusMove_NamesTheStatus", () => {
    expect(causeWords(en, { channel: "status_move", actor: "holger", source: { kind: "status", detail: "Ready" } }))
      .toBe("started by a status move to Ready by holger");
    expect(causeWords(en, { channel: "pickup", actor: "holger", source: { kind: "status", detail: "Ready" } }))
      .toBe("picked up in Ready by holger");
  });

  it("CauseWords_UnknownChannel_NamesTheChannel", () => {
    expect(causeWords(en, { channel: "carrier_pigeon", actor: null, source: null })).toBe("started via carrier_pigeon");
  });
});
