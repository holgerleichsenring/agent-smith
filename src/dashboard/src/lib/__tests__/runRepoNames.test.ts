import { describe, it, expect } from "vitest";
import { deriveRunRepoNames } from "@/lib/runRepoNames";
import { EventType, type RunEvent } from "@/types/hub-events";

function sandboxCreated(repo: string): RunEvent {
  return {
    type: EventType.SandboxCreated,
    runId: "r1",
    timestamp: "2026-07-06T00:00:00Z",
    repo,
    image: "img",
    language: "csharp",
  } as unknown as RunEvent;
}

function runStarted(repos: string[]): RunEvent {
  return {
    type: EventType.RunStarted,
    runId: "r1",
    timestamp: "2026-07-06T00:00:00Z",
    trigger: "t",
    pipeline: "fix-bug",
    repos,
    startedAt: "2026-07-06T00:00:00Z",
    agentName: null,
  } as unknown as RunEvent;
}

describe("deriveRunRepoNames", () => {
  it("uses the snapshot repos and IGNORES SandboxCreated composite sandbox keys", () => {
    // The regression: a repo with two toolchain/resource sandboxes emitted composite
    // keys (Sample.Server-c#-2-2gi / -3gi); those must not appear as repo badges.
    const events: RunEvent[] = [
      sandboxCreated("Sample.Server-c#-2-2gi"),
      sandboxCreated("Sample.Server-c#-2-3gi"),
      sandboxCreated("Sample.Client"),
    ];

    const result = deriveRunRepoNames(
      ["Sample.Server", "Sample.Client", "Sample.BackgroundWorker"],
      events,
    );

    expect(result).toEqual(["Sample.BackgroundWorker", "Sample.Client", "Sample.Server"]);
    expect(result).not.toContain("Sample.Server-c#-2-2gi");
    expect(result).not.toContain("Sample.Server-c#-2-3gi");
  });

  it("falls back to the RunStarted event repos when the snapshot has none", () => {
    const result = deriveRunRepoNames(undefined, [runStarted(["Sample.A", "Sample.B"])]);

    expect(result).toEqual(["Sample.A", "Sample.B"]);
  });

  it("dedupes snapshot + RunStarted and never surfaces sandbox keys", () => {
    const result = deriveRunRepoNames(
      ["Sample.Server"],
      [runStarted(["Sample.Server", "Sample.Client"]), sandboxCreated("Sample.Server-c#-2-2gi")],
    );

    expect(result).toEqual(["Sample.Client", "Sample.Server"]);
  });
});
