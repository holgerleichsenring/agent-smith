import { act, renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { PendingQuestionInfo } from "@/types/hub-events";

// p0458 / 2026-10-13-9a59p: the parked question's four states, now owned by a hook the ticket
// list's needs-you rows use.

const fetchRun = vi.fn();
vi.mock("@/lib/runsApi", () => ({ fetchRun: (...args: unknown[]) => fetchRun(...args) }));

import { useParkedQuestion } from "../useParkedQuestion";

const question: PendingQuestionInfo = {
  questionId: "q1",
  type: "Freeform",
  text: "Keep the outbox?",
  context: null,
  choices: [],
  defaultAnswer: null,
  askedAt: "2026-07-17T10:01:00Z",
  answerDeadlineAt: "2026-07-17T12:00:00Z",
};

beforeEach(() => fetchRun.mockReset());

describe("useParkedQuestion", () => {
  it("AnInlineQuestion_IsUsedWithoutFetching", () => {
    const { result } = renderHook(() => useParkedQuestion("r1", question));
    expect(result.current.state.kind).toBe("ready");
    expect(fetchRun).not.toHaveBeenCalled();
  });

  it("WithoutAnInlineQuestion_TheRunDetailIsFetchedOnce", async () => {
    fetchRun.mockResolvedValue({ pendingQuestion: question });
    const { result } = renderHook(() => useParkedQuestion("r1", null));
    await waitFor(() => expect(result.current.question).toEqual(question));
    expect(fetchRun).toHaveBeenCalledTimes(1);
  });

  it("ARunWithNothingToAnswer_IsUnavailable", async () => {
    fetchRun.mockResolvedValue({ pendingQuestion: null });
    const { result } = renderHook(() => useParkedQuestion("r1", null));
    await waitFor(() => expect(result.current.state.kind).toBe("unavailable"));
  });

  it("AnAcceptedAnswer_IsNeverReportedAsAMissingQuestion", () => {
    const { result, rerender } = renderHook(({ inline }) => useParkedQuestion("r1", inline), {
      initialProps: { inline: question as PendingQuestionInfo | null },
    });
    act(() => result.current.onAnswered());
    rerender({ inline: null });
    expect(result.current.state.kind).toBe("answered");
    expect(fetchRun).not.toHaveBeenCalled();
  });
});
