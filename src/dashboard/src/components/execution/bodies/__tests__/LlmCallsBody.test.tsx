import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { LlmCallsBody } from "../LlmCallsBody";
import type { PairedLlmCall } from "@/hooks/execution-tree/llmPairing";

function call(over: Partial<PairedLlmCall>): PairedLlmCall {
  return {
    id: "c",
    role: "coding-agent-master",
    roleIsUnknown: false,
    model: "claude-opus-4-8",
    phase: null,
    startedAt: "t0",
    finishedAt: "t1",
    durationMs: 1000,
    tokensIn: 10,
    tokensOut: 20,
    costUsd: 0.01,
    cachedTokensIn: null,
    cacheCreationTokensIn: null,
    cacheHit: false,
    ...over,
  };
}

describe("LlmCallsBody", () => {
  it("LlmStep_EveryTurn_HasActivityLabel", () => {
    render(
      <LlmCallsBody
        calls={[
          call({ id: "known", role: "auth-reviewer", roleIsUnknown: false }),
          call({ id: "unk-phase", role: "unknown", roleIsUnknown: true, phase: "Execute" }),
          call({ id: "unk-bare", role: "unknown", roleIsUnknown: true, phase: null }),
        ]}
      />,
    );

    const labels = screen.getAllByTestId("llm-call-activity");
    expect(labels).toHaveLength(3);
    expect(labels[0]).toHaveTextContent("auth-reviewer"); // known role
    expect(labels[1]).toHaveTextContent("Execute"); // unknown role falls back to phase
    expect(labels[2]).toHaveTextContent("llm call"); // unknown + no phase still labelled

    // No turn renders a bare "unknown" activity.
    labels.forEach((l) => expect(l.textContent).not.toBe("unknown"));
    expect(screen.queryByTestId("llm-call-role-unknown")).not.toBeInTheDocument();
  });

  it("LlmStep_UnfinishedCall_ShowsInFlight_WhileRunning", () => {
    render(<LlmCallsBody calls={[call({ id: "x", finishedAt: null })]} runEnded={false} />);
    expect(screen.getByText("in flight")).toBeInTheDocument();
    expect(screen.queryByText("ended")).not.toBeInTheDocument();
  });

  it("LlmRow_RendersCachedShare_WhenCachedTokensPresent", () => {
    // p0323: 6200 cached of (3300 + 6200 + 500) = 62% — the per-call cached
    // share that makes a dead cache visible.
    render(
      <LlmCallsBody
        calls={[call({ id: "c1", tokensIn: 3300, cachedTokensIn: 6200, cacheCreationTokensIn: 500 })]}
      />,
    );
    expect(screen.getByTestId("llm-call-c1-cached-share")).toHaveTextContent("62% cached");
  });

  it("LlmRow_OmitsCachedShare_WhenNoCachedTokens", () => {
    render(<LlmCallsBody calls={[call({ id: "c2", cachedTokensIn: 0 })]} />);
    expect(screen.queryByTestId("llm-call-c2-cached-share")).not.toBeInTheDocument();
  });

  it("LlmStep_UnfinishedCall_FlipsToEnded_OnTerminalRun", () => {
    // p0227: a canceled/failed run leaves cut-off calls without a Finished
    // event — they must read neutral "ended", not a pulsing "in flight".
    render(<LlmCallsBody calls={[call({ id: "x", finishedAt: null })]} runEnded={true} />);
    expect(screen.getByText("ended")).toBeInTheDocument();
    expect(screen.queryByText("in flight")).not.toBeInTheDocument();
  });
});
