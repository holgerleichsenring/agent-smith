import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { RunStepRow } from "@/lib/runStepsApi";
import { EventType, type RunEvent } from "@/types/hub-events";
import type { LogStep } from "../runLogSections";

// 2026-11-18-e06be: a step's model calls as a numbered sequence — each with what it was fed and what it decided, the
// newest 20 first shown, earlier ones on request, and the prompt opening under its call.

const digest = vi.fn();
const prompt = vi.fn();
vi.mock("@/lib/runStoryApi", () => ({
  fetchPromptDigest: (...a: unknown[]) => digest(...a),
  fetchCallPrompt: (...a: unknown[]) => prompt(...a),
}));
let page: { events: RunEvent[]; hasOlder: boolean; loading: boolean; loadOlder: () => void };
vi.mock("@/hooks/useRunStepEvents", () => ({ useRunStepEvents: () => page }));
vi.mock("@/hooks/useAnalyzeMarkdown", () => ({ useAnalyzeMarkdown: () => ({ content: null, loading: false }) }));
vi.mock("@/hooks/useSandboxEvents", () => ({ useSandboxEvents: () => ({ outputs: [] }) }));

const { callSequence, tokenWords, spendWords } = await import("../callSequence");
const { StepCallSequence } = await import("../StepCallSequence");
const { RunStepDetail } = await import("../RunStepDetail");

const ev = (o: Record<string, unknown>): RunEvent => ({ runId: "r1", ...o }) as unknown as RunEvent;
const at = (s: number) => new Date(Date.UTC(2026, 10, 18, 10, 0, s)).toISOString();

/** n calls, 10 s apart: each reads a file after it finishes, and the result comes back before the next one. */
function calls(n: number): RunEvent[] {
  return Array.from({ length: n }, (_, i) => [
    ev({ type: EventType.LlmCallStarted, timestamp: at(i * 10), model: "gpt-5", role: "master", promptHash: "h", repoName: null }),
    ev({ type: EventType.LlmCallFinished, timestamp: at(i * 10 + 4), model: "gpt-5", role: "master", tokensIn: 12400, tokensOut: 410,
      costUsd: 0.031, durationMs: 4200, repoName: null, cachedTokensIn: 9800, promptId: `p${i + 1}` }),
    ev({ type: EventType.ToolCall, timestamp: at(i * 10 + 5), tool: "ReadFile", argsLength: 10, summary: `src/File${i + 1}.cs`, intent: null, role: null, repoName: null }),
    ev({ type: EventType.ToolResult, timestamp: at(i * 10 + 6), tool: "ReadFile", ok: true, resultLength: 1200, errorMessage: null, role: null, repoName: null }),
  ]).flat();
}

beforeEach(() => {
  digest.mockReset();
  prompt.mockReset();
  digest.mockResolvedValue([]);
  prompt.mockResolvedValue({ id: "p30", toolCount: 3, complete: true, blocks: [{ position: 0, kind: "Message", role: "user", chars: 12, content: "Implement it" }] });
});

describe("callSequence", () => {
  it("callSequence_NumbersOldestFirst_AndJoinsTheToolEvents", () => {
    const seq = callSequence(calls(3), 3, true);
    expect(seq.map((c) => c.n)).toEqual([1, 2, 3]);
    expect(seq[0].fed).toBeNull();
    expect(seq[1].fed).toBe("ReadFile result, 1,200 chars");
    expect(seq[1].decided).toBe("ReadFile src/File2.cs");
    expect(seq[2].promptId).toBe("p3");
    expect(tokenWords(seq[0])).toBe("12,400 in (9,800 cache) / 410 out");
    expect(spendWords(seq[0], true)).toBe("$0.031 · 4.2s");
  });

  it("callSequence_OlderEventsNotLoaded_NumbersFromTheStepCount", () => {
    expect(callSequence(calls(3), 168, false).map((c) => c.n)).toEqual([166, 167, 168]);
    expect(callSequence(calls(3), null, false).map((c) => c.n)).toEqual([null, null, null]);
  });
});

describe("StepCallSequence", () => {
  it("StepCallSequence_ThirtyCalls_ShowsTheNewestTwenty_ThenEarlier", () => {
    render(<StepCallSequence runId="r1" events={calls(30)} total={30} hasOlder={false} loading={false} loadOlder={vi.fn()} runEnded />);
    const shown = screen.getAllByTestId("step-call");
    expect(shown).toHaveLength(20);
    expect(shown[0]).toHaveAttribute("data-n", "11");
    expect(shown[19]).toHaveTextContent("#30master · gpt-512,400 in (9,800 cache) / 410 out$0.031 · 4.2s");
    fireEvent.click(screen.getByTestId("step-calls-earlier"));
    expect(screen.getAllByTestId("step-call")).toHaveLength(30);
    expect(screen.queryByTestId("step-calls-earlier")).toBeNull();
  });

  it("StepCallSequence_PageExhausted_LoadsOlderEvents", () => {
    const loadOlder = vi.fn();
    render(<StepCallSequence runId="r1" events={calls(5)} total={40} hasOlder loading={false} loadOlder={loadOlder} runEnded />);
    fireEvent.click(screen.getByTestId("step-calls-earlier"));
    expect(loadOlder).toHaveBeenCalledOnce();
  });

  it("StepCallSequence_DigestLines_ReplaceTheEventFallbacks", async () => {
    digest.mockResolvedValue([{ promptId: "p2", fed: "ReadFile result: namespace Shop;", decided: "WriteFile src/A.cs" }]);
    render(<StepCallSequence runId="r1" events={calls(2)} total={2} hasOlder={false} loading={false} loadOlder={vi.fn()} runEnded />);
    expect(digest).toHaveBeenCalledWith("r1", ["p1", "p2"], expect.anything());
    const second = () => screen.getAllByTestId("step-call")[1];
    await waitFor(() => expect(second().querySelector("[data-testid=step-call-fed]")).toHaveTextContent("← ReadFile result: namespace Shop;"));
    expect(second().querySelector("[data-testid=step-call-decided]")).toHaveTextContent("→ WriteFile src/A.cs");
    expect(screen.getAllByTestId("step-call")[0].querySelector("[data-testid=step-call-decided]")).toHaveTextContent("→ ReadFile src/File1.cs");
  });

  it("StepCallSequence_ACall_OpensItsPromptInPlace", async () => {
    render(<StepCallSequence runId="r1" events={calls(2)} total={2} hasOlder={false} loading={false} loadOlder={vi.fn()} runEnded />);
    fireEvent.click(screen.getAllByTestId("step-call")[1].querySelector("button")!);
    expect(prompt).toHaveBeenCalledWith("r1", "p2", expect.anything());
    expect(await screen.findByTestId("call-prompt")).toHaveTextContent("Call 2 was sent 1 blocks");
  });
});

describe("RunStepDetail LLM filter", () => {
  it("RunStepDetail_TheLlmFilter_ShowsTheCallSequence", () => {
    page = { events: calls(3), hasOlder: false, loading: false, loadOlder: vi.fn() };
    const row = { stepIndex: 7, stepName: "Run master skill", displayName: "Run master skill", commandName: "AgenticMaster", status: "success",
      durationSeconds: 60, resultMessage: null, llmCalls: 3, costUsd: 0.09, sandboxCommands: 0, subAgents: 0 } as RunStepRow;
    render(<RunStepDetail runId="r1" step={{ row, label: "Run master skill", state: "done", story: true } as LogStep} live={false} />);
    fireEvent.click(screen.getByTestId("step-filter-llm"));
    expect(screen.getAllByTestId("step-call")).toHaveLength(3);
    expect(screen.queryByTestId("step-detail-line")).toBeNull();
  });
});
