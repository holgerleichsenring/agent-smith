import { beforeEach, describe, expect, it, vi } from "vitest";
import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { ProjectInitAction } from "../ProjectInitAction";
import { getJobsHubClient } from "@/lib/JobsHubClient";
import { HUB_URL } from "@/hooks/useJobsHub";
import type { LiveInitRun } from "@/lib/projectInitApi";

// 2026-10-02-5f89d: the button reads the project's live init run from the server, so a
// navigation away and back — an unmount and a remount — no longer forgets a running init.

const RUN = "2026-10-02T09-43-00-5f89";
const api = vi.hoisted(() => ({
  fetchProjectInit: vi.fn<(project: string) => Promise<LiveInitRun | null>>(),
  startProjectInit: vi.fn(),
}));

vi.mock("@/lib/projectInitApi", () => api);

const link = () => screen.findByTestId("project-init-running-sample");

describe("ProjectInitAction restores the live run", () => {
  beforeEach(() => {
    api.fetchProjectInit.mockReset();
    api.startProjectInit.mockReset();
  });

  it("ProjectInitAction restores 'Initializing — view run' after a remount and shows cancelling for a flagged run", async () => {
    api.fetchProjectInit.mockResolvedValue({ runId: RUN, state: "running" });
    const first = render(<ProjectInitAction project="sample" />);
    expect((await link()).textContent).toBe("Initializing — view run");
    first.unmount();

    render(<ProjectInitAction project="sample" />);
    const restored = await link();
    expect(restored.textContent).toBe("Initializing — view run");
    expect(restored.getAttribute("href")).toBe(`/jobs/${RUN}`);
  });

  it("Restore_AFlaggedRun_ShowsCancelling", async () => {
    api.fetchProjectInit.mockResolvedValue({ runId: RUN, state: "cancelling" });

    render(<ProjectInitAction project="sample" />);

    expect((await link()).textContent).toBe("Cancelling — view run");
  });

  it("Restore_AQueuedRun_ShowsWaitingForASlot", async () => {
    api.fetchProjectInit.mockResolvedValue({ runId: RUN, state: "queued" });

    render(<ProjectInitAction project="sample" />);

    expect((await link()).textContent).toBe("Waiting for a slot — view run");
  });

  it("Restore_NoLiveRun_ShowsInitialize", async () => {
    api.fetchProjectInit.mockResolvedValue(null);

    render(<ProjectInitAction project="sample" />);

    await waitFor(() => expect(api.fetchProjectInit).toHaveBeenCalledWith("sample", expect.anything()));
    expect(screen.getByTestId("project-init-sample").textContent).toBe("Initialize");
  });

  it("RunsChanged_ForTheLiveRun_ReReadsAndFollowsItToItsEnd", async () => {
    api.fetchProjectInit.mockResolvedValueOnce({ runId: RUN, state: "running" });
    render(<ProjectInitAction project="sample" />);
    await link();

    api.fetchProjectInit.mockResolvedValue(null);
    act(() => getJobsHubClient(HUB_URL).runsChanged.emit(RUN));

    await waitFor(() => expect(screen.getByTestId("project-init-sample").textContent).toBe("Initialize"));
  });

  it("Press_ShowsTheServersAnswerForTheStartedRun", async () => {
    api.fetchProjectInit.mockResolvedValueOnce(null);
    api.startProjectInit.mockResolvedValue({ outcome: "started", runId: RUN, reason: null });
    render(<ProjectInitAction project="sample" />);
    await waitFor(() => expect(api.fetchProjectInit).toHaveBeenCalledTimes(1));

    api.fetchProjectInit.mockResolvedValue({ runId: RUN, state: "queued" });
    fireEvent.click(screen.getByTestId("project-init-sample"));

    expect((await link()).textContent).toBe("Waiting for a slot — view run");
  });

  it("Press_WhenTheReReadFails_StillLinksTheStartedRun", async () => {
    api.fetchProjectInit.mockRejectedValue(new Error("403"));
    api.startProjectInit.mockResolvedValue({ outcome: "started", runId: RUN, reason: null });
    render(<ProjectInitAction project="sample" />);

    fireEvent.click(screen.getByTestId("project-init-sample"));

    expect((await link()).textContent).toBe("Initializing — view run");
  });
});
