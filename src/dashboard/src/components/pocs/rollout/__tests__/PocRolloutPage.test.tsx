import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { PocRevisionView, PocRolloutView } from "@/lib/pocsApi";
import type { RunStepRow } from "@/lib/runStepsApi";
import { EventType, type RunEvent } from "@/types/hub-events";
import { PocRolloutPage, rolloutEnded } from "../PocRolloutPage";

// 2026-11-23-c81ec: the Rollout page lists the PoC run's steps in order with the host pipeline's three jobs and its link,
// the GitOps commit's link, the rollout step's one line per Argo CD reading, and the PoC's address once it settled.

const getRollout = vi.fn();
const getRevision = vi.fn();
const fetchSteps = vi.fn();
const fetchPage = vi.fn();
const fetchDelta = vi.fn();

vi.mock("@/lib/pocsApi", () => ({
  getPocRollout: (...args: unknown[]) => getRollout(...args),
  getPocRevision: (...args: unknown[]) => getRevision(...args),
}));

vi.mock("@/lib/runStepsApi", () => ({
  fetchRunSteps: (...args: unknown[]) => fetchSteps(...args),
  fetchRunStepEventPage: (...args: unknown[]) => fetchPage(...args),
  fetchRunStepEventDelta: (...args: unknown[]) => fetchDelta(...args),
}));

function rollout(over: Partial<PocRolloutView> = {}): PocRolloutView {
  return {
    pocId: 4, name: "todo", runId: "r-7", revision: 2, state: "ready", rolloutState: "healthy",
    rolloutAt: "2026-11-23T10:05:00Z", url: "https://todo.poc.example.internal",
    pipelineUrl: "https://git.example.internal/platform/poc-source/actions/runs/91",
    sourceCommitSha: "1111111aaaaaaa", gitopsCommitSha: "2222222bbbbbbb",
    gitopsCommitUrl: "https://git.example.internal/platform/poc-source/commit/2222222bbbbbbb", ...over,
  };
}

function step(stepIndex: number, commandName: string, status: string | null = "success", planned = false): RunStepRow {
  return {
    stepIndex, stepName: commandName, displayName: null, commandName, planned, status, durationSeconds: 1, resultMessage: null,
    llmCalls: 0, costUsd: 0, sandboxCommands: 0, subAgents: 0,
  };
}

const STEPS = [
  step(0, "PocIntakeCommand"), step(1, "PocCodeScanCommand"), step(2, "PocSourceCommitCommand"),
  step(3, "PocPipelineBuildCommand"), step(4, "SealPocVariablesCommand"), step(5, "GitOpsCommitCommand"),
  step(6, "PocRolloutFollowCommand"), step(7, "WriteRunResult"),
];

const REVISION = {
  pocId: 4, name: "todo", revision: 2, latestRevision: 2, state: "built", runId: "r-7", commitSha: "1111111aaaaaaa",
  verdict: { blocks: [], notes: [], isBlocked: false, overrulable: false }, explanation: null, mayOverrule: false, whyNot: null,
  digest: "sha256:" + "c".repeat(64),
} as PocRevisionView;

function reading(detail: string, at: string): RunEvent {
  return { runId: "r-7", type: EventType.L1StepDetail, timestamp: at, stepIndex: 6, origin: "PocRolloutFollow", detail } as RunEvent;
}

beforeEach(() => {
  for (const mock of [getRollout, getRevision, fetchSteps, fetchPage, fetchDelta]) mock.mockReset();
  getRevision.mockResolvedValue(REVISION);
  fetchSteps.mockResolvedValue(STEPS);
  fetchPage.mockImplementation(async (_run: string, stepIndex: number) => ({
    events: stepIndex === 6
      ? [
          reading("sync Synced, health Progressing, image matches, checksum matches, generation 3/3, replicas 0/1", "2026-11-23T10:04:50Z"),
          reading("sync Synced, health Healthy, image matches, checksum matches, generation 3/3, replicas 1/1", "2026-11-23T10:05:00Z"),
        ]
      : [],
    oldestSeq: 1, newestSeq: 2, hasOlder: false,
  }));
});

describe("PocRolloutPage", () => {
  it("PocRolloutPage_Settled_ListsTheStepsWithThePipelineJobsTheCommitLinkAndTheUrl", async () => {
    getRollout.mockResolvedValue(rollout());
    render(<PocRolloutPage pocId={4} runId="r-7" />);

    expect(await screen.findByTestId("poc-rollout")).toBeInTheDocument();
    expect(getRollout).toHaveBeenCalledWith(4, "r-7", expect.anything());
    const steps = screen.getByTestId("poc-rollout-steps").querySelectorAll(":scope > li");
    expect([...steps].map((li) => li.getAttribute("data-testid"))).toEqual([
      "poc-rollout-step-PocIntake", "poc-rollout-step-PocCodeScan", "poc-rollout-step-PocSourceCommit",
      "poc-rollout-step-PocPipelineBuild", "poc-rollout-step-SealPocVariables", "poc-rollout-step-GitOpsCommit",
      "poc-rollout-step-PocRolloutFollow",
    ]);
    expect(screen.getByTestId("poc-rollout-step-PocIntake")).toHaveTextContent("Validate");
    expect(screen.getByTestId("poc-rollout-step-PocRolloutFollow")).toHaveTextContent("Rollout");

    expect(await screen.findByTestId("poc-rollout-step-PocPipelineBuild-jobs-build")).toHaveAttribute("data-look", "done");
    expect(screen.getByTestId("poc-rollout-step-PocPipelineBuild-jobs-imageScan")).toHaveTextContent("Image scan");
    expect(screen.getByTestId("poc-rollout-step-PocPipelineBuild-jobs-push")).toHaveAttribute("data-look", "done");
    expect(screen.getByTestId("poc-rollout-step-PocPipelineBuild-pipeline")).toHaveAttribute("href",
      "https://git.example.internal/platform/poc-source/actions/runs/91");
    expect(screen.getByTestId("poc-rollout-step-GitOpsCommit-commit")).toHaveAttribute("href",
      "https://git.example.internal/platform/poc-source/commit/2222222bbbbbbb");
    expect(screen.getByTestId("poc-rollout-step-GitOpsCommit-commit")).toHaveTextContent("2222222");
    expect(screen.getByTestId("poc-rollout-step-PocSourceCommit-sha")).toHaveTextContent("1111111");

    const lines = await screen.findByTestId("poc-rollout-step-PocRolloutFollow-lines");
    const items = lines.querySelectorAll("li");
    expect(items).toHaveLength(2);
    expect(items[0]).toHaveTextContent("health Progressing");
    expect(items[1]).toHaveTextContent("health Healthy");

    expect(screen.getByTestId("poc-rollout-state")).toHaveTextContent("Healthy");
    expect(screen.getByTestId("poc-rollout-url")).toHaveAttribute("href", "https://todo.poc.example.internal");
  });

  it("PocRolloutPage_StillFollowing_ShowsNoUrl_AndACommitWithoutUrlAsItsSha", async () => {
    getRollout.mockResolvedValue(rollout({ rolloutState: null, url: null, gitopsCommitUrl: null, rolloutAt: null }));
    fetchSteps.mockResolvedValue([...STEPS.slice(0, 6), step(6, "PocRolloutFollowCommand", "running")]);
    render(<PocRolloutPage pocId={4} runId="r-7" />);

    expect(await screen.findByTestId("poc-rollout-state")).toHaveTextContent("Following");
    expect(screen.queryByTestId("poc-rollout-url")).not.toBeInTheDocument();
    const commit = screen.getByTestId("poc-rollout-step-GitOpsCommit-commit");
    expect(commit.tagName).toBe("SPAN");
    expect(commit).toHaveTextContent("2222222");
    expect(screen.getByTestId("poc-rollout-step-PocRolloutFollow-look")).toHaveTextContent("running");
  });

  it("PocRolloutPage_NotFound_SaysSo", async () => {
    getRollout.mockResolvedValue(null);
    render(<PocRolloutPage pocId={4} runId="r-x" />);

    await waitFor(() => expect(screen.getByTestId("poc-rollout-missing")).toHaveTextContent("does not exist"));
    expect(fetchSteps).not.toHaveBeenCalled();
  });

  it("rolloutEnded_IsTrueOnceSettledOrAStepFailed_FalseWhileAStepRuns", () => {
    expect(rolloutEnded(rollout(), STEPS)).toBe(true);
    const following = rollout({ rolloutState: null });
    expect(rolloutEnded(following, [step(0, "PocIntakeCommand"), step(1, "PocCodeScanCommand", "running")])).toBe(false);
    expect(rolloutEnded(following, [step(0, "PocIntakeCommand", "failed"), step(1, "PocCodeScanCommand", null, true)])).toBe(true);
    expect(rolloutEnded(following, [])).toBe(false);
  });
});
