import { act, renderHook } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { useSandboxActivity } from "../useSandboxActivity";
import { getJobsHubClient, __resetJobsHubClientForTests } from "@/lib/JobsHubClient";
import type { SandboxActivityRollup } from "@/types/hub-events";

const HUB_URL = process.env.NEXT_PUBLIC_HUB_URL ?? "/hub/jobs";

function rollup(runId: string, commands: number, lastCommand: string): SandboxActivityRollup {
  return { runId, repo: "repo", commands, lastCommand, lastSummary: null, timestamp: "2026-07-22T13:00:00Z" };
}

describe("useSandboxActivity", () => {
  afterEach(() => __resetJobsHubClientForTests());

  it("returns the latest rollup for the matching run only", () => {
    const client = getJobsHubClient(HUB_URL);
    const { result } = renderHook(() => useSandboxActivity("run-A"));

    act(() => client.sandboxActivity.emit(rollup("run-B", 9, "grep")));
    expect(result.current).toBeNull(); // different run — ignored

    act(() => client.sandboxActivity.emit(rollup("run-A", 3, "dotnet build")));
    expect(result.current?.commands).toBe(3);

    act(() => client.sandboxActivity.emit(rollup("run-A", 7, "dotnet test")));
    expect(result.current?.commands).toBe(7); // latest wins
    expect(result.current?.lastCommand).toBe("dotnet test");
  });

  it("resets to null when the runId changes", () => {
    const client = getJobsHubClient(HUB_URL);
    const { result, rerender } = renderHook(({ id }) => useSandboxActivity(id), {
      initialProps: { id: "run-A" },
    });
    act(() => client.sandboxActivity.emit(rollup("run-A", 4, "ReadFile")));
    expect(result.current?.commands).toBe(4);

    rerender({ id: "run-C" });
    expect(result.current).toBeNull();
  });
});
