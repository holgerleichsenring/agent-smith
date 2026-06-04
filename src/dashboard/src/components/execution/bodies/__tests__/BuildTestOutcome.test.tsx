import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { StepSandboxes } from "@/components/execution/bodies/StepSandboxes";
import { EventFilterProvider } from "@/lib/EventFilterContext";
import type { SandboxRepoSnapshot } from "@/hooks/useRunExecutionTree";

vi.mock("@/hooks/useSandboxEvents", () => ({
  useSandboxEvents: () => ({ command: null, outputs: [] }),
}));
vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: vi.fn() }),
  usePathname: () => "/jobs/r-1",
  useSearchParams: () => new URLSearchParams(),
}));

function snapshot(repo: string, exitCode: number | null): SandboxRepoSnapshot {
  return { repo, command: "dotnet test", commandSummary: "dotnet test", exitCode, durationMs: 2300 };
}

describe("Build/test outcome (p0222)", () => {
  it("RunDetail_BuildTestOutcome_VisibleAtAGlance", () => {
    render(
      <EventFilterProvider>
        <StepSandboxes runId="r-1" sandboxes={[snapshot("server", 0), snapshot("client", 1)]} />
      </EventFilterProvider>,
    );

    // The outcome is phrased explicitly — "passed" / "failed", not a raw exit code.
    expect(screen.getByTestId("step-sandbox-status-server")).toHaveTextContent("dotnet test");
    expect(screen.getByTestId("step-sandbox-status-server")).toHaveTextContent("passed");
    expect(screen.getByTestId("step-sandbox-status-client")).toHaveTextContent("failed (exit 1)");
  });

  it("RunDetail_CouldNotRunSentinel_RendersNeutralNotRun_NotRed", () => {
    // p0227: exit -1 is the JobLoop "couldn't run" sentinel (untouched repo /
    // unreachable sandbox / canceled run), not a real failure — it must read
    // neutral, not alarming red.
    render(
      <EventFilterProvider>
        <StepSandboxes runId="r-1" sandboxes={[snapshot("server", -1)]} />
      </EventFilterProvider>,
    );

    const row = screen.getByTestId("step-sandbox-status-server");
    expect(row).toHaveTextContent("not run");
    expect(row).not.toHaveTextContent("failed");
    expect(row.className).toContain("text-stone-500");
    expect(row.className).not.toContain("text-rose-700");
  });
});
