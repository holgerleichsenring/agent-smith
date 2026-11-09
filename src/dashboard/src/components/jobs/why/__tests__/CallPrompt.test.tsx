import { describe, it, expect, vi, afterEach, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { CallTable } from "../CallTable";
import type { RunCallPoint } from "@/lib/runStoryApi";

// 2026-11-09-0620a: a call's prompt opens below the call table, its blocks in send order.

const call: RunCallPoint = {
  index: 3, specId: null, stepIndex: 4, role: "design-partner", model: "m", promptChars: 1200,
  answerChars: 40, durationMs: 900, throttleWaitMs: 0, outcome: "Ok", attempt: 1,
  promptId: "abc", toolCount: 2, tokensIn: 100, cachedTokensIn: 300,
};

const fetchMock = vi.fn();

beforeEach(() => {
  fetchMock.mockReset();
  vi.stubGlobal("fetch", fetchMock);
});

afterEach(() => vi.unstubAllGlobals());

describe("CallTable prompt", () => {
  it("the call table opens a call's prompt blocks in send order", async () => {
    fetchMock.mockResolvedValue({ ok: true, json: async () => ({
      id: "abc", toolCount: 2, complete: true,
      blocks: [
        { position: 0, kind: "Tools", role: "", chars: 10, content: "## read_file" },
        { position: 1, kind: "Message", role: "system", chars: 6, content: "system" },
        { position: 2, kind: "Message", role: "user", chars: 2, content: "go" },
      ],
    }) });

    render(<CallTable runId="run-1" calls={[call]} />);
    expect(screen.getByText("75%")).toBeTruthy();
    fireEvent.click(screen.getByTestId("call-prompt-open"));

    await waitFor(() => expect(screen.getAllByTestId("call-prompt-block")).toHaveLength(3));
    const roles = screen.getAllByTestId("call-prompt-block").map((b) => b.getAttribute("data-role"));
    expect(roles).toEqual(["", "system", "user"]);
    expect(screen.getByTestId("call-prompt").textContent).toContain("offered 2 tools");
  });

  it("a call without a stored prompt shows its size as plain text", () => {
    render(<CallTable runId="run-1" calls={[{ ...call, promptId: null }]} />);
    expect(screen.queryByTestId("call-prompt-open")).toBeNull();
  });
});
