import { describe, expect, it } from "vitest";
import type { RunStepRow } from "@/lib/runStepsApi";
import { EventType, type RunEvent, type RunSnapshot } from "@/types/hub-events";
import type { TicketAttempt } from "@/types/tickets";
import { endedView, liveLines, nowLabel, nowMode, pullRequestLinks, waitLines } from "../runNowLines";

// 2026-11-17-6d9ea: the Now block's lines — live from the run's hub events, and how a finished run ended.

function snap(over: Partial<RunSnapshot> = {}): RunSnapshot {
  return {
    runId: "r1", pipeline: "fix-bug", trigger: "ticket", repos: ["server"], status: "running", prUrl: null, summary: null,
    startedAt: "2026-10-13T06:00:00Z", finishedAt: null, sandboxes: 1, stepIndex: 4, stepName: "Run master skill", totalSteps: 12,
    lastEventType: null, costUsd: 8.69, llmCalls: 12, ticketId: null, ticketTitle: null, agentName: null,
    cancelRequested: false, ...over,
  };
}

function row(stepIndex: number, commandName: string, over: Partial<RunStepRow> = {}): RunStepRow {
  return {
    stepIndex, stepName: commandName, displayName: commandName, commandName, status: "success", durationSeconds: 10,
    resultMessage: null, llmCalls: 0, costUsd: 0, sandboxCommands: 0, subAgents: 0, ...over,
  };
}

const base = { runId: "r1" };
const started = (stepIndex: number, displayName: string, timestamp: string): RunEvent =>
  ({ ...base, type: EventType.StepStarted, timestamp, stepIndex, stepName: displayName, totalSteps: 12, displayName });
const decision = (chose: string, timestamp: string): RunEvent =>
  ({ ...base, type: EventType.DecisionLogged, timestamp, category: "Implementation", chose, over: null, reason: "r" });
const wrote = (path: string, timestamp: string): RunEvent =>
  ({ ...base, type: EventType.SubAgentFileWritten, timestamp, subAgentId: "s", path, bytes: 120 });
const tool = (intent: string | null, timestamp: string): RunEvent =>
  ({ ...base, type: EventType.ToolCall, timestamp, tool: "run_command", argsLength: 3, summary: "dotnet build", intent, role: null, repoName: null }) as unknown as RunEvent;

describe("runNowLines", () => {
  it("nowMode_And_Label_FollowTheState", () => {
    expect(nowLabel(snap())).toBe("Now — follows the agent");
    expect(nowMode(snap({ status: "waiting_for_input" }))).toBe("waiting");
    expect(nowMode(snap({ status: "waiting_for_input", queuePosition: 2 }))).toBe("queued");
    expect(nowLabel(snap({ status: "success" }))).toBe("How it ended");
  });

  it("liveLines_TheStepWithItsNarration_ThenTheNewestActs", () => {
    const events = [
      started(4, "Run master skill", "2026-10-13T13:40:00Z"),
      tool("building the server after converting the enums", "2026-10-13T13:49:40Z"),
      wrote("src/Query.cs", "2026-10-13T13:49:58Z"),
      decision("Convert enums to integer ids", "2026-10-13T13:50:41Z"),
    ];
    const lines = liveLines(snap(), events, [row(4, "AgenticMaster", { status: "running", displayName: "Run master skill" })], { lastCommand: "dotnet test" });
    expect(lines[0]).toMatchObject({ kind: "Run master skill", text: "building the server after converting the enums", tone: "now" });
    expect(lines[1]).toMatchObject({ kind: "ran", text: "dotnet test" });
    expect(lines.slice(2).map((l) => l.kind)).toEqual(["decision", "wrote", "tool"]);
    expect(lines[2].text).toBe("Convert enums to integer ids");
  });

  it("liveLines_ANewHubEvent_MovesToTheTop", () => {
    const events = [started(4, "Run master skill", "2026-10-13T13:40:00Z"), decision("first", "2026-10-13T13:41:00Z")];
    expect(liveLines(snap(), events, [], null)[1].text).toBe("first");
    const more = [...events, wrote("src/B.cs", "2026-10-13T13:42:00Z")];
    expect(liveLines(snap(), more, [], null)[1]).toMatchObject({ kind: "wrote", text: "src/B.cs (120 bytes)" });
    expect(liveLines(snap(), more, [], null)[0].kind).toBe("Run master skill");
  });

  it("waitLines_SayWhatItWaitsFor", () => {
    expect(waitLines(snap({ status: "waiting_for_input" }))[0].text).toMatch(/Reserved compute is held/);
    expect(waitLines(snap({ status: "queued", queuePosition: 2 }))[0].text).toBe("Place 2 in line. Nothing for you to do.");
    expect(waitLines(snap({ status: "waiting_for_input", queuePosition: 3 }))[0].text).toBe("Answered, place 3 in line. Nothing for you to do.");
  });

  it("endedView_Success_SaysPullRequestsCriteriaCost_ReviewAndVerify", () => {
    const s = snap({ status: "success", finishedAt: "2026-10-13T14:11:02Z",
      pullRequests: [{ repo: "server", url: "https://example.test/server/pull/1", status: "opened", isDraft: false }],
      acceptance: { criteria: [{ text: "a", status: "met", reason: null, citation: null }, { text: "b", status: "met", reason: null, citation: null }], outcome: null, ratifiedBy: null } });
    const pass: RunEvent = { ...base, type: EventType.VerifyPassRecorded, timestamp: "2026-10-13T14:06:31Z", result: "passed",
      repositories: [{ repository: "server", result: "passed", stages: [{ stage: "build", exitCode: 0, skipped: false }] }], staleDerivations: [] } as unknown as RunEvent;
    const view = endedView(s, [row(9, "ReviewSpecDiff", { resultMessage: "diff reviewed, no finding standing" })], [pass], null);
    expect(view.lines[0]).toMatchObject({ kind: "outcome", text: "Done · pull request opened · 2 of 2 criteria proven · $8.69", tone: "ok" });
    expect(view.lines.find((l) => l.kind === "review")?.text).toBe("diff reviewed, no finding standing");
    expect(view.verify).toBe(pass);
  });

  it("endedView_Failed_SaysWhyWhereAndWhatToDo", () => {
    const attempt = { number: 1, runId: "r1", status: "failed", endKind: "preflight", endedOnInfrastructure: false, handedBack: false,
      endDetail: { subject: "registry", control: "registries.trusted", setTo: "true", settingsPath: "/settings/registries" } } as unknown as TicketAttempt;
    const view = endedView(snap({ status: "failed", summary: "Preflight refused the image." }),
      [row(2, "RunPreflight", { status: "failed", displayName: "Check preconditions", resultMessage: "image not trusted" })], [], attempt);
    expect(view.lines[0]).toMatchObject({ text: "Stuck: a precondition did not hold · no pull request opened · $8.69", tone: "bad" });
    expect(view.lines.find((l) => l.kind === "why")?.text).toBe("Preflight refused the image.");
    expect(view.lines.find((l) => l.kind === "stopped at")?.text).toBe("Check preconditions: image not trusted");
    expect(view.lines.find((l) => l.kind === "to do")?.text).toMatch(/^Set registries.trusted to true/);
    expect(view.settingsPath).toBe("/settings/registries");
  });

  it("endedView_Cancelled_And_Shortfall", () => {
    expect(endedView(snap({ status: "cancelled", summary: "Cancelled by the operator." }), [], [], null).lines[0])
      .toMatchObject({ text: "Cancelled · $8.69", tone: "dim" });
    expect(endedView(snap({ status: "shortfall", prUrl: "https://example.test/server/pull/2" }), [], [], null).lines[0].text)
      .toBe("Done, with a shortfall · pull request opened · $8.69");
  });

  it("pullRequestLinks_ListElsePrUrl_PlusUrlsInTheSummary", () => {
    expect(pullRequestLinks(snap({ status: "success", prUrl: "https://example.test/a/pull/1" }))).toEqual([{ repo: "server", url: "https://example.test/a/pull/1", isDraft: false }]);
    expect(pullRequestLinks(snap({ status: "failed", summary: "draft kept at https://example.test/a/pull/3" }))).toEqual([{ repo: "", url: "https://example.test/a/pull/3", isDraft: true }]);
  });
});
