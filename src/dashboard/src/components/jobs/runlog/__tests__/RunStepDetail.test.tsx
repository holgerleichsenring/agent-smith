import { act, fireEvent, render, renderHook, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { RunStepRow } from "@/lib/runStepsApi";
import { EventType, type RunEvent } from "@/types/hub-events";
import type { LogStep } from "../runLogSections";

// 2026-11-17-6d9ec: an open step — filter links narrow the lines, show all lifts the cap, older events load on request,
// the Analyze step carries analyze.md; and the open step is the address, the graph view's old hash included.

const loadOlder = vi.fn();
let page: { events: RunEvent[]; hasOlder: boolean; loading: boolean; loadOlder: () => void };
vi.mock("@/hooks/useRunStepEvents", () => ({ useRunStepEvents: () => page }));
vi.mock("@/hooks/useAnalyzeMarkdown", () => ({ useAnalyzeMarkdown: () => ({ content: "# Understood\nthe server is .NET", loading: false }) }));
vi.mock("@/hooks/useSandboxEvents", () => ({ useSandboxEvents: () => ({ command: null, result: null, outputs: [], expanded: false }) }));

const { RunStepDetail } = await import("../RunStepDetail");
const { stepFromAddress, useStepAddress } = await import("../useStepAddress");

const ev = (o: Record<string, unknown>): RunEvent => ({ runId: "r1", ...o }) as unknown as RunEvent;
const at = (s: number) => `2026-10-13T13:${String(10 + Math.floor(s / 60)).padStart(2, "0")}:${String(s % 60).padStart(2, "0")}Z`;

function step(commandName: string, over: Partial<RunStepRow> = {}): LogStep {
  const row: RunStepRow = { stepIndex: 24, stepName: commandName, displayName: commandName, commandName, status: "success", durationSeconds: 60,
    resultMessage: null, llmCalls: 1, costUsd: 0.04, sandboxCommands: 0, subAgents: 0, ...over };
  return { row, label: commandName, state: "done", story: true };
}

beforeEach(() => {
  loadOlder.mockReset();
  page = {
    events: [
      ev({ type: EventType.DecisionLogged, timestamp: at(1), category: "c", chose: "Use the outbox", over: null, reason: "r" }),
      ev({ type: EventType.SubAgentFileWritten, timestamp: at(2), subAgentId: "s", path: "src/A.cs", bytes: 10 }),
      ...Array.from({ length: 14 }, (_, i) => ev({ type: EventType.ToolCall, timestamp: at(10 + i), tool: "read_file", argsLength: 1, summary: `file ${i}`, intent: null })),
    ],
    hasOlder: true, loading: false, loadOlder,
  };
});

describe("RunStepDetail", () => {
  it("RunStepDetail_FilterLinks_NarrowTheLines", () => {
    render(<RunStepDetail runId="r1" step={step("AgenticMaster")} live={false} />);
    expect(screen.getAllByTestId("step-detail-line")).toHaveLength(12);
    fireEvent.click(screen.getByTestId("step-filter-dec"));
    expect(screen.getAllByTestId("step-detail-line").map((l) => l.textContent)).toEqual([expect.stringContaining("Use the outbox")]);
    fireEvent.click(screen.getByTestId("step-filter-file"));
    expect(screen.getAllByTestId("step-detail-line")[0]).toHaveTextContent("src/A.cs");
    expect(screen.getByTestId("step-filter-file")).toHaveAttribute("aria-pressed", "true");
    fireEvent.click(screen.getByTestId("step-filter-all"));
    fireEvent.change(screen.getByTestId("step-find"), { target: { value: "file 3" } });
    expect(screen.getAllByTestId("step-detail-line")).toHaveLength(1);
  });

  it("RunStepDetail_ShowAll_LiftsTheCap_AndTheSummaryCounts", () => {
    render(<RunStepDetail runId="r1" step={step("AgenticMaster")} live={false} />);
    expect(screen.getByTestId("step-detail-summary")).toHaveTextContent("0 actions · 1 write · 0 llm · $0.00");
    fireEvent.click(screen.getByTestId("step-show-all"));
    expect(screen.getAllByTestId("step-detail-line")).toHaveLength(16);
    fireEvent.click(screen.getByTestId("step-order"));
    expect(screen.getAllByTestId("step-detail-line")[0]).toHaveTextContent("Use the outbox");
  });

  it("RunStepDetail_OlderEvents_LoadOnRequest", () => {
    render(<RunStepDetail runId="r1" step={step("AgenticMaster")} live={false} />);
    fireEvent.click(screen.getByTestId("step-events-load-older"));
    expect(loadOlder).toHaveBeenCalled();
  });

  it("RunStepDetail_AnalyzeCodebase_CarriesAnalyzeMd", () => {
    render(<RunStepDetail runId="r1" step={step("AnalyzeCode")} live={false} />);
    expect(screen.getByTestId("step-analyze")).toHaveTextContent("what the agent understood (analyze.md)");
  });

  it("RunStepDetail_APlannedStep_SaysItWasNotReached", () => {
    page = { events: [], hasOlder: false, loading: false, loadOlder };
    render(<RunStepDetail runId="r1" step={step("CommitAndPR", { planned: true, status: null })} live />);
    expect(screen.getByTestId("step-head-planned")).toHaveTextContent("The run has not reached this step yet.");
  });
});

describe("useStepAddress", () => {
  it("stepFromAddress_QueryFirst_ThenTheGraphViewsHash", () => {
    expect(stepFromAddress("?step=24", "")).toBe(24);
    expect(stepFromAddress("", "#n=step-24&x=step-3")).toBe(24);
    expect(stepFromAddress("", "#n=arch")).toBeNull();
  });

  it("useStepAddress_TheOldHash_IsRewrittenToStep", () => {
    window.history.replaceState(null, "", "/jobs/r1#n=step-24&x=step-3");
    const { result } = renderHook(() => useStepAddress());
    expect(result.current[0]).toBe(24);
    expect(window.location.search).toBe("?step=24");
    expect(window.location.hash).toBe("");
    act(() => result.current[1](7));
    expect(window.location.search).toBe("?step=7");
    act(() => result.current[1](null));
    expect(window.location.search).toBe("");
  });
});

// 2026-11-19-687eb: the step's sub-agents are rows under its header, before the event list, opened to brief and calls.
describe("RunStepDetail, sub-agents", () => {
  it("RunStepDetail_SubAgents_RowsBeforeTheList_ExpandToBriefAndCalls", () => {
    page = {
      events: [
        ev({ type: EventType.SubAgentSpawned, timestamp: at(1), subAgentId: "a", name: "Target architecture analyst", activity: "Map the target architecture",
          parentSubAgentId: null, inheritedContextHash: "x" }),
        ev({ type: EventType.SubAgentToolCall, timestamp: at(2), subAgentId: "a", toolName: "read_file", argsSummary: "src/arch.md" }),
        ev({ type: EventType.SubAgentCompleted, timestamp: at(60), subAgentId: "a", status: "Succeeded", observationsCount: 1, findingsCount: 0,
          filesWrittenCount: 0, toolCalls: 1, costUsd: 0.1 }),
        ev({ type: EventType.DecisionLogged, timestamp: at(61), category: "c", chose: "Use the outbox", over: null, reason: "r" }),
      ],
      hasOlder: false, loading: false, loadOlder,
    };
    render(<RunStepDetail runId="r1" step={step("AgenticMaster", { subAgents: 1 })} live={false} />);

    const row = screen.getByTestId("step-sub-agent");
    expect(row).toHaveTextContent("Target architecture analyst");
    expect(screen.getByTestId("step-sub-agent-status")).toHaveTextContent("finished · 1 tool call · 59.0s");
    expect(row.compareDocumentPosition(screen.getAllByTestId("step-detail-line")[0]) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(screen.queryByTestId("step-sub-agent-body")).toBeNull();

    fireEvent.click(screen.getByTestId("step-sub-agent-toggle"));

    expect(screen.getByTestId("step-sub-agent-brief")).toHaveTextContent("Map the target architecture");
    expect(screen.getAllByTestId("step-sub-agent-call").map((c) => c.textContent)).toEqual([expect.stringMatching(/read_file\s*src\/arch\.md/)]);
  });
});
