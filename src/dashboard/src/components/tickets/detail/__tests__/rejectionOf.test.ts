import { describe, expect, it } from "vitest";
import type { TicketSeriesCriterion } from "@/types/tickets";
import { rejectionOf } from "../specWords";

// 2026-10-16-86e6e: the gate's reason is the criterion's note on an unmet or unproven verdict.

const criterion = (verdict: string | null, note: string | null): TicketSeriesCriterion =>
  ({ text: "Every queue has a dead-letter queue", verdict, note, citation: null, overrule: null });

describe("rejectionOf", () => {
  it("rejectionOf_UnmetWithNote_IsTheNote", () => {
    expect(rejectionOf(criterion("unmet", "neither a file nor a command"))).toBe("neither a file nor a command");
    expect(rejectionOf(criterion("unproven", "nothing measured it"))).toBe("nothing measured it");
  });

  it("rejectionOf_MetOrNoNote_IsNull", () => {
    expect(rejectionOf(criterion("met", "the build exited 0"))).toBeNull();
    expect(rejectionOf(criterion("unmet", null))).toBeNull();
    expect(rejectionOf(criterion("unmet", "  "))).toBeNull();
  });
});
