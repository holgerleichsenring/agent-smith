import { describe, it, expect } from "vitest";
import { monotonizeBeats } from "../beatMonotonic";
import type { RunBeats } from "@/types/hub-events";

describe("monotonizeBeats", () => {
  it("Spine_BuildingNotDoneBeforePlanFinished", () => {
    // The observed defect: Building "done" while The plan is still "active".
    const beats: RunBeats = {
      ticket: "done",
      plan: "active",
      building: "done",
      verify: "pending",
      outcome: "pending",
    };
    const out = monotonizeBeats(beats);
    expect(out.plan).toBe("active");
    // Building cannot be done before Plan finishes — clamped to pending.
    expect(out.building).toBe("pending");
    expect(out.ticket).toBe("done");
  });

  it("clamps a later active beat when an earlier beat is pending", () => {
    const beats: RunBeats = {
      ticket: "done",
      plan: "pending",
      building: "active",
      verify: "pending",
      outcome: "pending",
    };
    expect(monotonizeBeats(beats).building).toBe("pending");
  });

  it("a failed beat blocks all later beats", () => {
    const beats: RunBeats = {
      ticket: "done",
      plan: "failed",
      building: "done",
      verify: "done",
      outcome: "done",
    };
    const out = monotonizeBeats(beats);
    expect(out.plan).toBe("failed");
    expect(out.building).toBe("pending");
    expect(out.verify).toBe("pending");
    expect(out.outcome).toBe("pending");
  });

  it("leaves a genuinely monotonic set untouched", () => {
    const beats: RunBeats = {
      ticket: "done",
      plan: "done",
      building: "active",
      verify: "pending",
      outcome: "pending",
    };
    expect(monotonizeBeats(beats)).toEqual(beats);
  });

  it("skipped counts as complete and does not block later beats", () => {
    const beats: RunBeats = {
      ticket: "done",
      plan: "skipped",
      building: "done",
      verify: "active",
      outcome: "pending",
    };
    expect(monotonizeBeats(beats)).toEqual(beats);
  });
});
