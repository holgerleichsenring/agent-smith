import { describe, expect, it } from "vitest";
import { deliveryPath } from "../deliveryApi";

// 2026-10-25-0099c: the period and every chosen project ride the query; no project means every readable one.

describe("deliveryApi", () => {
  it("DeliveryPath_PutsThePeriodAndEachChosenProjectInTheQuery", () => {
    expect(deliveryPath("week", 0, [])).toBe("/api/delivery?period=week");
    expect(deliveryPath("day", 0, ["web-shop", "team/alpha"])).toBe("/api/delivery?period=day&project=web-shop&project=team%2Falpha");
  });

  // 2026-11-12-aeddb: a past period rides as its offset; the current one sends none.
  it("DeliveryPath_PutsAPastPeriodsOffsetInTheQuery", () => {
    expect(deliveryPath("month", 2, ["web-shop"])).toBe("/api/delivery?period=month&offset=2&project=web-shop");
  });
});
