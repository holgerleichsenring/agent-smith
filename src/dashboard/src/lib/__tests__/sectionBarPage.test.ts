import { describe, expect, it } from "vitest";
import { hasSectionBar } from "../sectionBarPage";

describe("hasSectionBar", () => {
  it("hasSectionBar_TicketPageAndWorkItOut_HaveTheirOwnBar", () => {
    expect(hasSectionBar("/tickets/shop/101")).toBe(true);
    expect(hasSectionBar("/spec-dialog")).toBe(true);
  });

  it("hasSectionBar_ListsAndOtherPages_DoNot", () => {
    expect(hasSectionBar("/tickets")).toBe(false);
    expect(hasSectionBar("/spec-dialog/conversations")).toBe(false);
    expect(hasSectionBar("/runs")).toBe(false);
  });
});
