import { renderHook, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { HubConnectionState } from "@microsoft/signalr";
import { ApiRefusal, ApiResponseError } from "@/lib/apiResponse";

// 2026-09-21-291b: the fact was already in hand and thrown away. The hub cannot
// answer "why was I refused" — SignalR catches the HttpError carrying the status
// and rejects start() with prose — but the run fetch beside it rejects with a
// typed ApiRefusal, which this hook used to discard in a bare catch.

const fetchRuns = vi.hoisted(() => vi.fn());
vi.mock("@/lib/runsApi", () => ({ fetchRuns: (...a: unknown[]) => fetchRuns(...a) }));

function subject<T>() {
  const listeners = new Set<(v: T) => void>();
  return {
    add: (l: (v: T) => void) => { listeners.add(l); return () => listeners.delete(l); },
    emit: (v: T) => { for (const l of listeners) l(v); },
  };
}

// The nudge the server fires when a run changes — the hook's own way back to
// /api/runs, and therefore the only path on which a refusal can end.
const nudge = vi.hoisted(() => ({ fire: null as null | ((runId: string) => void) }));

vi.mock("@/lib/JobsHubClient", () => {
  const runsChanged = subject<string>();
  nudge.fire = runsChanged.emit;
  return {
    getJobsHubClient: () => ({
      state: () => HubConnectionState.Disconnected,
      connectionState: subject<HubConnectionState>(),
      systemActivityUpdates: subject<unknown>(),
      runsChanged,
      subscribeOverview: async () => async () => {},
    }),
    JobsHubClient: class {},
  };
});

import { useJobsHub } from "../useJobsHub";

const empty = { active: [], recent: [] };

describe("useJobsHub refusal", () => {
  beforeEach(() => {
    fetchRuns.mockReset();
  });

  it("JobsHub_RunFetchRefused_ReturnsTheRefusal", async () => {
    fetchRuns.mockRejectedValue(new ApiRefusal("/api/runs", 401, "sign-in", []));

    const { result } = renderHook(() => useJobsHub());

    await waitFor(() => expect(result.current.refusal).not.toBeNull());
    expect(result.current.refusal?.kind).toBe("sign-in");
  });

  it("JobsHub_RunFetchFailedWithoutARefusal_ReturnsNone", async () => {
    // A server that is genuinely down is not a refusal, and calling it one would
    // offer a sign-in button to somebody whose problem is a stopped process.
    fetchRuns.mockRejectedValue(new ApiResponseError("/api/runs", 500, "HTTP 500"));

    const { result } = renderHook(() => useJobsHub());

    await waitFor(() => expect(fetchRuns).toHaveBeenCalled());
    expect(result.current.refusal).toBeNull();
  });

  it("JobsHub_ALaterFetchSucceeds_ClearsTheRefusal", async () => {
    fetchRuns.mockRejectedValueOnce(new ApiRefusal("/api/runs", 401, "sign-in", []));

    const { result } = renderHook(() => useJobsHub());
    await waitFor(() => expect(result.current.refusal).not.toBeNull());

    // A fetch that LANDED is the only proof the refusal is over — clearing it on
    // anything else would clear it on the abort every nudge issues.
    fetchRuns.mockResolvedValue(empty);
    nudge.fire?.("r1");

    await waitFor(() => expect(result.current.refusal).toBeNull(), { timeout: 2000 });
  });
});
