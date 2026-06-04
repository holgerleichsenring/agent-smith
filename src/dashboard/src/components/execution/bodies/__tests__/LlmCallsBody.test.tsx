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
});
