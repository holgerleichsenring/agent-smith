import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { ActivityRow } from "../ActivityRow";
import { EventType, type RunEvent } from "@/types/hub-events";

describe("Typed step-detail rendering (p0173f step 5)", () => {
  it("L1StepDetailEvent_TypedRender_NotFreeFormString", () => {
    const event = {
      type: EventType.L1StepDetail,
      runId: "r-1",
      timestamp: "2026-05-20T10:00:00Z",
      stepIndex: 7,
      origin: "skill-round",
      detail: "architect: round 2 done (3 obs)",
    } as RunEvent;

    render(<ActivityRow event={event} />);

    // The dashboard shows the typed fields, not a JSON dump.
    expect(screen.getByText(/7/)).toBeInTheDocument();
    expect(screen.getByText(/skill-round/)).toBeInTheDocument();
    expect(screen.getByText(/architect: round 2 done/)).toBeInTheDocument();
    // No raw JSON braces of the event payload should appear inline.
    expect(document.body.textContent ?? "").not.toContain('"type":');
  });

  it("L1StepStartedEvent_TypedRender_WithCommandKind", () => {
    const event = {
      type: EventType.StepStarted,
      runId: "r-1",
      timestamp: "2026-05-20T10:00:00Z",
      stepIndex: 3,
      stepName: "AnalyzeCode",
      totalSteps: 12,
    } as RunEvent;

    render(<ActivityRow event={event} />);

    expect(screen.getByText(/3\/12 AnalyzeCode/)).toBeInTheDocument();
    expect(document.body.textContent ?? "").not.toContain('"runId"');
  });
});
