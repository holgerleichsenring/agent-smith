import { describe, expect, it } from "vitest";
import { causeWords, channelWords } from "../ticketFormat";

// 2026-10-13-8762q: an attempt an overrule started reads as one on the Attempts tab.

describe("ticketFormat", () => {
  it("channelWords_Overrule_IsOverruledDefault", () => {
    expect(channelWords("overrule")).toBe("overruled default");
    expect(causeWords({ channel: "overrule", actor: "m.reviewer", source: null })).toBe("overruled default by m.reviewer");
  });
});
