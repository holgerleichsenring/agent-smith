import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { ToolCallRow } from "../ToolCallRow";
import { ToolResultRow } from "../ToolResultRow";
import { EventType, type ToolCallEvent, type ToolResultEvent } from "@/types/hub-events";

const baseTime = new Date().toISOString();

const call: ToolCallEvent = {
  type: EventType.ToolCall,
  runId: "r",
  timestamp: baseTime,
  tool: "write_file",
  argsLength: 1234,
  summary: null,
};

const result: ToolResultEvent = {
  type: EventType.ToolResult,
  runId: "r",
  timestamp: baseTime,
  tool: "write_file",
  ok: true,
  resultLength: 42,
  errorMessage: null,
};

describe("ToolCallRow", () => {
  it("renders tool name + ArgsLength metadata, no content", () => {
    render(<ToolCallRow event={call} />);
    expect(screen.getByText("write_file")).toBeInTheDocument();
    expect(screen.getByText("(1234B args)")).toBeInTheDocument();
    expect(screen.getByTestId("metadata-tooltip")).toBeInTheDocument();
  });
});

describe("ToolResultRow", () => {
  it("renders ok + ResultLength metadata", () => {
    render(<ToolResultRow event={result} />);
    expect(screen.getByText("ok")).toBeInTheDocument();
    expect(screen.getByText("(42B result)")).toBeInTheDocument();
  });

  it("renders fail when ok=false", () => {
    render(<ToolResultRow event={{ ...result, ok: false }} />);
    expect(screen.getByText("fail")).toBeInTheDocument();
  });
});
