import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { EventType, type RunEvent } from "@/types/hub-events";
import { SubAgentObservationRow } from "../rows/SubAgentObservationRow";
import { SubAgentFindingRow } from "../rows/SubAgentFindingRow";
import { SubAgentFileWrittenRow } from "../rows/SubAgentFileWrittenRow";
import { SubAgentToolCallRow } from "../rows/SubAgentToolCallRow";
import { SubAgentTimelinePanel } from "../SubAgentTimelinePanel";

let runEventsForHook: RunEvent[] = [];

vi.mock("@/hooks/useRunEvents", () => ({
  useRunEvents: () => runEventsForHook,
}));

vi.mock("@/hooks/useJobsHub", () => ({
  useJobsHub: () => ({ client: null }),
}));

describe("SubAgentTimelinePanel and rows", () => {
  it("SubAgentObservationRow_TypedTextField_Rendered", () => {
    const event = {
      type: EventType.SubAgentObservation,
      runId: "r-1",
      timestamp: "2026-05-20T10:00:00Z",
      subAgentId: "sa-1",
      text: "the observation body",
    } as RunEvent & { text: string };
    render(<SubAgentObservationRow event={event} />);
    expect(screen.getByTestId("sub-agent-observation-text"))
      .toHaveTextContent("the observation body");
  });

  it("SubAgentFindingRow_SeverityBadgeTitleDetail_Rendered", () => {
    const event = {
      type: EventType.SubAgentFinding,
      runId: "r-1",
      timestamp: "2026-05-20T10:00:00Z",
      subAgentId: "sa-1",
      severity: "high",
      title: "the title",
      detail: "the detail",
    } as RunEvent & { severity: string; title: string; detail: string };
    render(<SubAgentFindingRow event={event} />);
    expect(screen.getByTestId("sub-agent-finding-severity")).toHaveTextContent(/high/i);
    expect(screen.getByTestId("sub-agent-finding-title")).toHaveTextContent("the title");
    expect(screen.getByTestId("sub-agent-finding-detail")).toHaveTextContent("the detail");
  });

  it("SubAgentFileWrittenRow_PathAndBytes_Rendered", () => {
    const event = {
      type: EventType.SubAgentFileWritten,
      runId: "r-1",
      timestamp: "2026-05-20T10:00:00Z",
      subAgentId: "sa-1",
      path: "src/Foo.cs",
      bytes: 1024,
    } as RunEvent & { path: string; bytes: number };
    render(<SubAgentFileWrittenRow event={event} />);
    expect(screen.getByTestId("sub-agent-file-path")).toHaveTextContent("src/Foo.cs");
    expect(screen.getByTestId("sub-agent-file-bytes")).toHaveTextContent("1024 B");
  });

  it("SubAgentToolCallRow_ToolNameAndArgsSummary_Rendered", () => {
    const event = {
      type: EventType.SubAgentToolCall,
      runId: "r-1",
      timestamp: "2026-05-20T10:00:00Z",
      subAgentId: "sa-1",
      toolName: "read_file",
      argsSummary: "read_file src/Foo.cs",
    } as RunEvent & { toolName: string; argsSummary: string };
    render(<SubAgentToolCallRow event={event} />);
    expect(screen.getByTestId("sub-agent-tool-name")).toHaveTextContent("read_file");
    expect(screen.getByTestId("sub-agent-tool-args")).toHaveTextContent("read_file src/Foo.cs");
  });

  it("SubAgentTimelinePanel_FiltersBySubAgentIdAndKind", () => {
    runEventsForHook = [
      {
        type: EventType.SubAgentObservation,
        runId: "r-1", timestamp: "2026-05-20T10:00:00Z",
        subAgentId: "sa-1", text: "first",
      } as RunEvent,
      {
        type: EventType.SubAgentFinding,
        runId: "r-1", timestamp: "2026-05-20T10:00:01Z",
        subAgentId: "sa-1", severity: "high", title: "the title", detail: "detail",
      } as RunEvent,
      {
        type: EventType.SubAgentObservation,
        runId: "r-1", timestamp: "2026-05-20T10:00:02Z",
        subAgentId: "sa-other", text: "other",
      } as RunEvent,
    ];

    render(<SubAgentTimelinePanel runId="r-1" subAgentId="sa-1" />);
    expect(screen.queryByText("first")).toBeInTheDocument();
    expect(screen.queryByText("the title")).toBeInTheDocument();
    expect(screen.queryByText("other")).not.toBeInTheDocument();

    // Click the finding-kind chip — narrow to findings only.
    fireEvent.click(screen.getByTestId("sub-agent-kind-chip-finding"));
    expect(screen.queryByText("first")).not.toBeInTheDocument();
    expect(screen.queryByText("the title")).toBeInTheDocument();
  });
});
