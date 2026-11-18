import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { EventType, type VerifyPassRecordedEvent } from "@/types/hub-events";
import { staleWords, stageWords, VerifyPassLines } from "../VerifyPassLines";

// 2026-11-17-d6fbc: a verify pass reads as a line per repository, the stale derivations as one muted line with the
// files behind a disclosure. 2026-11-18-d6d2e: moved here from the deleted RunSectionLog test.

const pass = {
  runId: "r1", type: EventType.VerifyPassRecorded, timestamp: "2026-11-17T09:00:00Z", originStepIndex: 3, result: "passed",
  repositories: [{ repository: "server", result: "passed", stages: [
    { stage: "install", command: "dotnet restore", exitCode: 0, skipped: false },
    { stage: "build", command: "dotnet build", exitCode: 0, skipped: false },
    { stage: "test", command: "dotnet test", exitCode: 0, skipped: false }] }],
  staleDerivations: [
    { repository: "server", context: "default", files: [".github/workflows/ci.yml"] },
    { repository: "server", context: "web", files: ["package.json"] }],
} as VerifyPassRecordedEvent;

describe("VerifyPassLines", () => {
  it("VerifyPassLines_ShowsARepositoryLineAndOneStaleLine", () => {
    render(<VerifyPassLines pass={pass} />);
    expect(screen.getByTestId("verify-pass-repo")).toHaveTextContent("✓serverinstall · build · test");
    const stale = screen.getByTestId("verify-pass-stale");
    expect(stale).not.toHaveAttribute("open");
    expect(stale.querySelector("summary")).toHaveTextContent(
      "2 contexts derived their verify steps from files that changed since; the stages ran as declared");
    expect(screen.getAllByTestId("verify-pass-stale-context")[1]).toHaveTextContent("server / web: package.json");
  });

  it("stageWords_MarksSkippedAndRedStages", () => {
    expect(stageWords({ repository: "client", result: "failed", stages: [
      { stage: "build", command: "b", exitCode: 0, skipped: false },
      { stage: "test", command: "t", exitCode: 1, skipped: false },
      { stage: "lint", command: "l", exitCode: 0, skipped: true }] })).toBe("build · test exited 1 · lint skipped");
    expect(staleWords(1)).toBe("1 context derived its verify steps from files that changed since; the stages ran as declared");
  });
});
