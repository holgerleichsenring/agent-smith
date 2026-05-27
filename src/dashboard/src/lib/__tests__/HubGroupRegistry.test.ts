import { describe, it, expect } from "vitest";
import { HubGroupRegistry } from "../HubGroupRegistry";

describe("HubGroupRegistry", () => {
  it("returns true on first incRef and false on subsequent ones", () => {
    const r = new HubGroupRegistry();
    expect(r.incRef("k")).toBe(true);
    expect(r.incRef("k")).toBe(false);
    expect(r.incRef("k")).toBe(false);
    expect(r.count("k")).toBe(3);
  });

  it("returns true only on the decRef that drops to zero", () => {
    const r = new HubGroupRegistry();
    r.incRef("k");
    r.incRef("k");
    expect(r.decRef("k")).toBe(false);
    expect(r.decRef("k")).toBe(true);
    expect(r.count("k")).toBe(0);
  });

  it("decRef on unknown key is a safe no-op", () => {
    const r = new HubGroupRegistry();
    expect(r.decRef("missing")).toBe(false);
  });

  it("reset clears all counters", () => {
    const r = new HubGroupRegistry();
    r.incRef("a");
    r.incRef("b");
    r.reset();
    expect(r.count("a")).toBe(0);
    expect(r.count("b")).toBe(0);
  });
});
