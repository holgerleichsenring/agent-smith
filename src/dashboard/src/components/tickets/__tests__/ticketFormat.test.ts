import { describe, expect, it } from "vitest";
import { causeWords, channelWords } from "../ticketFormat";

// 2026-10-13-8762q: an attempt an overrule started reads as one on the Attempts tab; 2026-10-13-8762w: one the
// series started by itself reads as an automatic series start.

describe("ticketFormat", () => {
  it("channelWords_Overrule_IsOverruledDefault", () => {
    expect(channelWords("overrule")).toBe("overruled default");
    expect(causeWords({ channel: "overrule", actor: "m.reviewer", source: null })).toBe("overruled default by m.reviewer");
  });

  it("ChannelWords_Series_IsAutomaticSeriesStart", () => {
    expect(channelWords("series")).toBe("series, started automatically");
    expect(causeWords({ channel: "series", actor: "agent-smith", source: null })).toBe("series, started automatically by agent-smith");
  });

  // 2026-11-16-f3fbc: an attempt agent-smith started once the precondition its predecessor stopped on held.
  it("ChannelWords_Precondition_IsRestartOncePreconditionHeld", () => {
    expect(channelWords("precondition")).toBe("restarted once the precondition held");
    expect(causeWords({ channel: "precondition", actor: null, source: null })).toBe("restarted once the precondition held");
  });
});
