import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { SandboxBox } from "../SandboxBox";
import { EventType, type SandboxOutputEvent } from "@/types/hub-events";
import * as useSandboxEventsModule from "@/hooks/useSandboxEvents";
import type { SandboxFeed } from "@/hooks/useSandboxEvents";

const feed = (output: SandboxOutputEvent[] = []): SandboxFeed => ({
  command: null,
  result: null,
  outputs: output,
  expanded: true,
});

beforeEach(() => {
  vi.restoreAllMocks();
});

describe("SandboxBox", () => {
  it("calls onToggle when the header is clicked", () => {
    vi.spyOn(useSandboxEventsModule, "useSandboxEvents").mockReturnValue(feed());
    const onToggle = vi.fn();
    render(<SandboxBox runId="r" repo="server" expanded={false} onToggle={onToggle} />);
    fireEvent.click(screen.getByRole("button"));
    expect(onToggle).toHaveBeenCalledTimes(1);
  });

  it("renders stdout lines when expanded", () => {
    const lines: SandboxOutputEvent[] = [{
      type: EventType.SandboxOutput,
      runId: "r",
      timestamp: new Date().toISOString(),
      repo: "server",
      stream: "stdout",
      line: "compiling",
      batchSeq: 1,
    }];
    vi.spyOn(useSandboxEventsModule, "useSandboxEvents").mockReturnValue(feed(lines));
    render(<SandboxBox runId="r" repo="server" expanded={true} onToggle={() => {}} />);
    expect(screen.getByText("compiling")).toBeInTheDocument();
  });

  it("renders a stderr line in error styling", () => {
    const lines: SandboxOutputEvent[] = [{
      type: EventType.SandboxOutput,
      runId: "r",
      timestamp: new Date().toISOString(),
      repo: "server",
      stream: "stderr",
      line: "warning: foo",
      batchSeq: 1,
    }];
    vi.spyOn(useSandboxEventsModule, "useSandboxEvents").mockReturnValue(feed(lines));
    render(<SandboxBox runId="r" repo="server" expanded={true} onToggle={() => {}} />);
    const el = screen.getByText("warning: foo");
    expect(el.className).toContain("rose");
  });
});
