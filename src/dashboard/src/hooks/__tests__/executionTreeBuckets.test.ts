import { describe, it, expect } from "vitest";
import { statusFromString } from "../execution-tree/buckets";

describe("statusFromString", () => {
  it("maps a sub-agent that answered nothing to shortfall, not wait", () => {
    expect(statusFromString("Empty")).toBe("shortfall");
    expect(statusFromString("empty")).toBe("shortfall");
  });

  it("keeps succeeded and failed as they were", () => {
    expect(statusFromString("Succeeded")).toBe("ok");
    expect(statusFromString("Failed")).toBe("fail");
  });
});
