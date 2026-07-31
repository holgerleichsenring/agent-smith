import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { RunStory } from "../RunStory";
import type { RunSnapshot } from "@/types/hub-events";

// p0390: the Plan beat shows the WORK SPEC above the plan — what this run must
// make true, and every revision with the cause that produced it. A run that
// derived no spec must look exactly as it did before, so the panel is absent
// rather than an empty card.

const specMarkdown = { current: null as string | null };

vi.mock("@/hooks/useJobsHub", () => ({
  useJobsHub: () => ({
    client: {
      getResultMarkdown: () => Promise.resolve(null),
      getPlanMarkdown: () => Promise.resolve(null),
      getAnalyzeMarkdown: () => Promise.resolve(null),
      getSpecMarkdown: () => Promise.resolve(specMarkdown.current),
    },
    connectionState: 1,
    overview: null,
    systemActivity: null,
  }),
}));

beforeEach(() => {
  specMarkdown.current = null;
  vi.stubGlobal("fetch", vi.fn(async () => ({ ok: true, json: async () => ({}) })));
});

const BEATS = {
  ticket: "done",
  plan: "active",
  building: "pending",
  verify: "pending",
  outcome: "pending",
} as const;

function snap(): RunSnapshot {
  return {
    runId: "r1", pipeline: "fix-bug", trigger: "ticket", repos: ["server"],
    status: "running", prUrl: null, summary: null,
    startedAt: "2026-07-17T10:00:00Z", finishedAt: null, sandboxes: 1,
    stepIndex: 3, stepName: null, totalSteps: 9, lastEventType: null,
    costUsd: 0, llmCalls: 0, ticketId: null, ticketTitle: null,
    agentName: null, cancelRequested: false,
    beats: BEATS,
  } as RunSnapshot;
}

describe("the Plan beat's work spec", () => {
  it("renders the current revision and the revision list when a spec was derived", async () => {
    specMarkdown.current =
      "# Reject empty payloads\n\n## Requirements\n- An empty body returns 400.\n\n"
      + "## Revisions\n- **1** — initial derivation\n- **2** — reviewer edit on the ticket branch\n";

    render(<RunStory runId="r1" snapshot={snap()} events={[]} />);
    fireEvent.click(screen.getByTestId("story-beat-plan"));

    await waitFor(() => expect(screen.getByTestId("spec-panel")).toBeInTheDocument());
    expect(screen.getByTestId("spec-markdown")).toHaveTextContent("An empty body returns 400.");
    expect(screen.getByTestId("spec-markdown")).toHaveTextContent(
      "reviewer edit on the ticket branch");
  });

  it("shows no spec card at all when the run derived none", async () => {
    render(<RunStory runId="r1" snapshot={snap()} events={[]} />);
    fireEvent.click(screen.getByTestId("story-beat-plan"));

    await waitFor(() => expect(screen.getByTestId("plan-panel")).toBeInTheDocument());
    expect(screen.queryByTestId("spec-panel")).not.toBeInTheDocument();
  });
});
