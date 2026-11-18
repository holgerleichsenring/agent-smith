import { describe, expect, it } from "vitest";
import { EventType, type RunEvent } from "@/types/hub-events";
import { repoOutcomeWords, repoSummary, stepDetailLines } from "../stepDetailLines";

// 2026-11-17-6d9ec: a step's events as terminal lines, with the summary the command timeline counted.

const at = (s: number) => `2026-10-13T13:50:${String(s).padStart(2, "0")}Z`;
const ev = (o: Record<string, unknown>): RunEvent => ({ runId: "r1", ...o }) as unknown as RunEvent;

const events: RunEvent[] = [
  ev({ type: EventType.LlmCallStarted, timestamp: at(1), model: "gpt-5", role: "agentic-executor", promptHash: "h", repoName: null }),
  ev({ type: EventType.LlmCallFinished, timestamp: at(7), model: "gpt-5", role: "agentic-executor", tokensIn: 9606, tokensOut: 552, costUsd: 0.038,
    durationMs: 5800, repoName: null, cachedTokensIn: 8100 }),
  ev({ type: EventType.SandboxCommand, timestamp: at(10), repo: "server", command: "run_command", argsLength: 10, summary: "dotnet build", isWrite: false }),
  ev({ type: EventType.SandboxResult, timestamp: at(51), repo: "server", command: "run_command", exitCode: 0, durationMs: 41000 }),
  ev({ type: EventType.SandboxCommand, timestamp: at(52), repo: "server", command: "WriteFile", argsLength: 10, summary: "src/Query.cs" }),
  ev({ type: EventType.SandboxResult, timestamp: at(53), repo: "server", command: "WriteFile", exitCode: 0, durationMs: 12 }),
  ev({ type: EventType.SandboxCommand, timestamp: at(54), repo: "client", command: "run_command", argsLength: 10, summary: "npm test" }),
  ev({ type: EventType.SandboxResult, timestamp: at(55), repo: "client", command: "run_command", exitCode: 1, durationMs: 300000, timedOut: true, timeoutSeconds: 300 }),
  ev({ type: EventType.DecisionLogged, timestamp: at(56), category: "Implementation", chose: "Convert enums to ids", over: null, reason: "EF Core 9 comparer" }),
  ev({ type: EventType.SubAgentSpawned, timestamp: at(57), subAgentId: "s1", name: "tester", activity: "write tests", parentSubAgentId: null, inheritedContextHash: "x" }),
  ev({ type: EventType.SubAgentFinding, timestamp: at(58), subAgentId: "s1", severity: "high", title: "SQL Server migrations invalid", detail: "under SQLite" }),
  ev({ type: EventType.SubAgentFileWritten, timestamp: at(59), subAgentId: "s1", path: "tests/QueryTests.cs", bytes: 900 }),
];

describe("stepDetailLines", () => {
  it("stepDetailLines_EveryKind_NewestFirst", () => {
    const view = stepDetailLines(events, true);
    expect(view.lines.map((l) => l.kind)).toEqual(["write", "finding", "sub-agent", "decision", "tool", "write", "tool", "llm"]);
    const llm = view.lines.find((l) => l.kind === "llm")!;
    expect(llm.text).toBe("agentic-executor · gpt-5 · 9,606 in / 552 out · cache 8,100");
    expect(llm.meta).toBe("$0.038 · 5.8s");
    expect(view.lines.find((l) => l.text.startsWith("server run_command"))?.meta).toBe("exit 0 · 41.0s");
    expect(view.lines.find((l) => l.text.startsWith("client"))).toMatchObject({ meta: "timed out · 5m 0s (limit 300s)", tone: "bad" });
    expect(view.lines.find((l) => l.kind === "finding")).toMatchObject({ text: "tester: high · SQL Server migrations invalid — under SQLite", tone: "bad", filter: "find" });
    expect(view.lines[0]).toMatchObject({ text: "tester: tests/QueryTests.cs", meta: "900 bytes", filter: "file" });
  });

  it("stepDetailLines_Summary_CountsActionsWritesLlmCost", () => {
    const view = stepDetailLines(events, true);
    expect([view.actions, view.writes, view.llm, view.costUsd]).toEqual([3, 2, 1, 0.038]);
  });

  it("stepDetailLines_AnUnfinishedCall_IsInFlightWhileLive_EndedAfter", () => {
    const start = [events[0]];
    expect(stepDetailLines(start, false).lines[0].meta).toBe("in flight");
    expect(stepDetailLines(start, true).lines[0].meta).toBe("ended");
  });

  it("stepDetailLines_PullRequestAndCatalog_CarryTheirLinks", () => {
    const view = stepDetailLines([
      ev({ type: EventType.PullRequestOutcome, timestamp: at(1), repo: "server", status: "opened", url: "https://example.test/server/pull/4", reason: null }),
      ev({ type: EventType.CatalogLoaded, timestamp: at(2), version: "v5.34.0", source: "Embedded", sourceUrl: "u", conceptCount: 3, skillsLoaded: 40, mastersCount: 4, fromCache: true, durationMs: 12 }),
    ], true);
    expect(view.lines[0]).toMatchObject({ kind: "catalog", href: "/system/catalog", text: "v5.34.0 from embedded · 3 concepts · 40 skills · 4 masters · warm cache" });
    expect(view.lines[1]).toMatchObject({ kind: "pr", href: "https://example.test/server/pull/4", filter: null });
  });

  it("repoSummary_And_repoOutcomeWords", () => {
    const view = stepDetailLines(events, true);
    expect(repoSummary(view.repos)).toBe("1/2 repositories ok, failed: client");
    expect(repoOutcomeWords({ repo: "a", summary: "x", exitCode: -1, durationMs: null })).toBe("not run");
    expect(repoSummary(view.repos.slice(0, 1))).toBeNull();
  });
});
