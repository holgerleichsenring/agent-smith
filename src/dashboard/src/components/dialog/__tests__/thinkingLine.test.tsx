import { act, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { SpecDialogActivityPush } from "@/types/spec-dialog";
import { condenseSteps, inFlight, type RawStep } from "../condenseSteps";
import { DialogWorking } from "../DialogWorking";

// 2026-11-19-687ea: a model call in flight is the working view's last line, ticking, until the next step arrives.

const TURN = "2026-11-19T10:00:00Z";

function push(seq: number, kind: SpecDialogActivityPush["kind"], name: string | null, detail: string | null = null): SpecDialogActivityPush {
  return { dialogId: "d", turnStartedAt: TURN, at: TURN, seq, kind, name, detail };
}

describe("condenseSteps and thinking steps", () => {
  it("drops thinking steps and keeps a read burst whole across them", () => {
    const raw: RawStep[] = [
      { seq: 1, kind: "thinking", name: "m" },
      { seq: 2, kind: "tool", name: "read_file", detail: "a.cs" },
      { seq: 3, kind: "thinking", name: "m" },
      { seq: 4, kind: "tool", name: "read_file", detail: "b.cs" },
      { seq: 5, kind: "thinking", name: "Analyst › m" },
    ];

    expect(condenseSteps(raw).map((line) => line.text)).toEqual(["2 reads"]);
  });
});

describe("inFlight", () => {
  it("names the master's call, a sub-agent's call, and nothing once another step follows", () => {
    expect(inFlight([{ seq: 1, kind: "thinking", name: "gpt-x" }])).toEqual({ key: 1, who: null });
    expect(inFlight([{ seq: 2, kind: "thinking", name: "PoC analyst › gpt-x" }])).toEqual({ key: 2, who: "PoC analyst" });
    expect(inFlight([{ seq: 1, kind: "thinking", name: "gpt-x" }, { seq: 2, kind: "tool", name: "write_file" }])).toBeNull();
    expect(inFlight([])).toBeNull();
  });
});

describe("DialogWorking, a call in flight", () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it("shows 'thinking · Ns' as the last line, ticking", () => {
    render(<DialogWorking readings={[]} since={null}
      activity={[push(1, "tool", "write_file", "draft:a.yaml"), push(2, "model", "gpt-x"), push(3, "thinking", "gpt-x")]} />);

    expect(screen.getByTestId("dialog-thinking")).toHaveTextContent("thinking · 0s");
    act(() => { vi.advanceTimersByTime(14_000); });
    expect(screen.getByTestId("dialog-thinking")).toHaveTextContent("thinking · 14s");
    expect(screen.getByTestId("dialog-activity").lastElementChild).toBe(screen.getByTestId("dialog-thinking"));
  });

  it("prefixes a sub-agent's call with its name", () => {
    render(<DialogWorking readings={[]} since={null} activity={[push(1, "thinking", "PoC analyst › gpt-x")]} />);

    expect(screen.getByTestId("dialog-thinking")).toHaveTextContent("PoC analyst › thinking · 0s");
  });

  it("shows no thinking line once the next step arrived", () => {
    render(<DialogWorking readings={[]} since={null}
      activity={[push(1, "thinking", "gpt-x"), push(2, "model", "gpt-x"), push(3, "tool", "read_file", "a.cs")]} />);

    expect(screen.queryByTestId("dialog-thinking")).toBeNull();
    expect(screen.getAllByTestId("dialog-activity-line").map((line) => line.textContent)).toEqual(["1 read"]);
  });
});
