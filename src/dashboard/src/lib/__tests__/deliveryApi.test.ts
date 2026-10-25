import { describe, expect, it } from "vitest";
import { deliveryPath } from "../deliveryApi";

// 2026-10-25-0099c: the period and every chosen project ride the query; no project means every readable one.

describe("deliveryApi", () => {
  it("DeliveryPath_PutsThePeriodAndEachChosenProjectInTheQuery", () => {
    expect(deliveryPath("week", [])).toBe("/api/delivery?period=week");
    expect(deliveryPath("day", ["web-shop", "team/alpha"])).toBe("/api/delivery?period=day&project=web-shop&project=team%2Falpha");
  });
});
