import { describe, expect, it } from "vitest";
import type { TicketDerivation } from "@/types/tickets";
import { attemptListLine, derivationListLine, runListLines } from "../runLines";
import { attempt } from "./detailFixtures";

// 2026-11-17-8d1cc: the Runs section's lines from recorded fields — every attempt says how it ended with its end kind.

const derivation = (over: Partial<TicketDerivation> = {}): TicketDerivation => ({
  runId: "d-1", status: "success", startedAt: "2026-09-04T09:00:00Z", finishedAt: "2026-09-04T09:04:10Z", cost: 0.42,
  outcome: "derived", specCount: 1, pullRequests: [], summary: null, ...over,
});

describe("runLines", () => {
  it("attemptListLine_NamesOutcomeEndKindFirstLineDurationCostAndTheRun", () => {
    const line = attemptListLine(attempt({ status: "failed", endKind: "bootstrap-missing", summary: "Pipeline aborted\nmore", cost: 0.71 }));
    expect(line).toMatchObject({ who: "Run 1", outcome: "failed · bootstrap missing", text: "Pipeline aborted", tone: "bad", href: "/jobs/r-1" });
    expect(line.meta).toMatch(/ · 30m 0s · \$0\.71$/);
  });

  it("derivationListLine_SaysWhatCameOfIt_SoFarWhileRunning", () => {
    expect(derivationListLine(derivation())).toMatchObject({ who: "Derivation", outcome: "derived", text: "Spec derivation found 1 spec.", tone: "ok", attempt: null });
    expect(derivationListLine(derivation({ outcome: "running", finishedAt: null })).meta).toMatch(/\$0\.42 so far$/);
  });

  it("runListLines_AreNewestStartFirst", () => {
    const lines = runListLines(
      [attempt({ number: 1, runId: "r-1", startedAt: "2026-09-04T10:00:00Z" }), attempt({ number: 2, runId: "r-2", startedAt: "2026-09-04T12:00:00Z" })],
      [derivation({ startedAt: "2026-09-04T11:00:00Z" })],
    );
    expect(lines.map((l) => l.runId)).toEqual(["r-2", "d-1", "r-1"]);
  });
});
