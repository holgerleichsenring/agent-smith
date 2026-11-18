import { useState } from "react";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { EventType, type RunEvent, type VerifyPassRecordedEvent } from "@/types/hub-events";
import { staleWords, stageWords } from "../VerifyPassLines";
import { RunSectionLog } from "../RunSectionLog";
import { runSections, type SectionId } from "../runSections";

// 2026-11-16-6877b: the sectioned log as a person meets it — every section closed, a strip node opens its section,
// the check section hosts what it is given.

const steps = [
  { stepIndex: 1, stepName: "FetchTicket", displayName: "Fetch ticket", commandName: "FetchTicket", status: "success", durationSeconds: 28,
    resultMessage: "GH-13 fetched", llmCalls: 0, costUsd: 0, sandboxCommands: 0, subAgents: 0 },
  { stepIndex: 2, stepName: "ReviewSpecDiff", displayName: "Review the spec's diff", commandName: "ReviewSpecDiff", status: "success",
    durationSeconds: 170, resultMessage: "1 finding left open", hasFinding: true, llmCalls: 3, costUsd: 0.1, sandboxCommands: 0, subAgents: 0 },
];

function Log() {
  const [open, setOpen] = useState<ReadonlySet<SectionId>>(new Set());
  const toggle = (id: SectionId) => setOpen((now) => {
    const next = new Set(now);
    if (!next.delete(id)) next.add(id);
    return next;
  });
  const sections = runSections(steps, [], "done", "2 of 2 done criteria met");
  return (
    <RunSectionLog sections={sections} open={open} onToggle={toggle} slots={{ check: <div data-testid="criteria-here">criteria</div> }} />
  );
}

describe("RunSectionLog", () => {
  it("RunSectionLog_EverySectionStartsClosed_WithTitleSummaryAndTime", () => {
    render(<Log />);
    expect(screen.getAllByRole("button", { expanded: false })).toHaveLength(7);
    expect(screen.queryAllByTestId("run-log-line")).toHaveLength(0);
    const ready = screen.getByTestId("run-section-ready");
    expect(ready).toHaveTextContent("Got ready");
    expect(ready).toHaveTextContent("GH-13 fetched");
    expect(ready).toHaveTextContent("28 s");
    expect(screen.getByTestId("run-section-review")).toHaveAttribute("data-state", "warn");
    expect(screen.getByTestId("run-section-review")).toHaveTextContent("2 min 50 s");
  });

  it("RunSectionLog_TheHeaderOpensItsSection_AndClosesIt", () => {
    render(<Log />);
    expect(screen.getByTestId("run-section-deliver")).toHaveAttribute("data-state", "pending");
    fireEvent.click(within(screen.getByTestId("run-section-review")).getByRole("button", { expanded: false }));
    const lines = within(screen.getByTestId("run-section-review")).getAllByTestId("run-log-line");
    expect(lines[0]).toHaveTextContent("Review the spec's diff: 1 finding left open");
    fireEvent.click(within(screen.getByTestId("run-section-review")).getByRole("button", { expanded: true }));
    expect(screen.queryAllByTestId("run-log-line")).toHaveLength(0);
  });

  it("RunSectionLog_CheckSection_HostsTheCriteria", () => {
    render(<Log />);
    fireEvent.click(within(screen.getByTestId("run-section-check")).getByRole("button", { expanded: false }));
    expect(within(screen.getByTestId("run-section-check")).getByTestId("criteria-here")).toBeInTheDocument();
  });
});

// 2026-11-17-d6fbc: a verify pass reads as a line per repository under its Build and test line, the stale derivations
// as one muted line with the files behind a disclosure, and the prose detail is not drawn.
describe("RunSectionLog verify pass", () => {
  const verifyStep = {
    stepIndex: 3, stepName: "VerifySpec", displayName: "Build and test", commandName: "VerifySpec", status: "success",
    durationSeconds: 40, resultMessage: "Verified: server [install+build+test] green\n(default): context 'default' derived its verify block from ...",
    llmCalls: 0, costUsd: 0, sandboxCommands: 3, subAgents: 0,
  };
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

  function VerifyLog({ events }: { events: RunEvent[] }) {
    const sections = runSections([verifyStep], events, "pending", "");
    return <RunSectionLog sections={sections} open={new Set<SectionId>(["verify"])} onToggle={() => {}} slots={{}} />;
  }

  it("VerifyLine_WithItsPass_ShowsARepositoryLineAndOneStaleLine_NotTheProse", () => {
    render(<VerifyLog events={[pass]} />);
    const line = screen.getByTestId("run-log-line");
    expect(line).toHaveTextContent("Build and test passed");
    expect(line).not.toHaveTextContent("Verified: server");
    expect(screen.getByTestId("verify-pass-repo")).toHaveTextContent("✓serverinstall · build · test");
    const stale = screen.getByTestId("verify-pass-stale");
    expect(stale).not.toHaveAttribute("open");
    expect(stale.querySelector("summary")).toHaveTextContent(
      "2 contexts derived their verify steps from files that changed since; the stages ran as declared");
    expect(screen.getAllByTestId("verify-pass-stale-context")[1]).toHaveTextContent("server / web: package.json");
  });

  it("VerifyLine_WithoutItsPass_KeepsTheProse", () => {
    render(<VerifyLog events={[]} />);
    expect(screen.getByTestId("run-log-line")).toHaveTextContent("Build and test: Verified: server [install+build+test] green");
    expect(screen.queryByTestId("verify-pass")).not.toBeInTheDocument();
  });

  it("stageWords_MarksSkippedAndRedStages", () => {
    expect(stageWords({ repository: "client", result: "failed", stages: [
      { stage: "build", command: "b", exitCode: 0, skipped: false },
      { stage: "test", command: "t", exitCode: 1, skipped: false },
      { stage: "lint", command: "l", exitCode: 0, skipped: true }] })).toBe("build · test exited 1 · lint skipped");
    expect(staleWords(1)).toBe("1 context derived its verify steps from files that changed since; the stages ran as declared");
  });
});
