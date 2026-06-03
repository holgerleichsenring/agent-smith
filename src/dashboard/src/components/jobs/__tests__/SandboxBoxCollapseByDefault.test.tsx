import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { StepSandboxes } from "@/components/execution/bodies/StepSandboxes";
import { EventFilterProvider } from "@/lib/EventFilterContext";
import type { SandboxRepoSnapshot } from "@/hooks/useRunExecutionTree";

// p0203 (6) — stdout collapsed by default on success, auto-expanded on
// failure; placeholder replaced with explicit "click to expand" affordance.

vi.mock("@/hooks/useSandboxEvents", () => ({
  useSandboxEvents: () => ({ command: null, outputs: [] }),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: vi.fn() }),
  usePathname: () => "/jobs/run-1",
  useSearchParams: () => new URLSearchParams(),
}));

function snapshot(repo: string, exitCode: number | null, durationMs: number | null): SandboxRepoSnapshot {
  return { repo, command: "npm test", commandSummary: "npm test", exitCode, durationMs };
}

describe("SandboxBox collapse-by-default (p0203)", () => {
  it("SandboxBox_StdoutCollapsedOnSuccess_ExpandedOnFailure", () => {
    render(
      <EventFilterProvider>
        <StepSandboxes
          runId="r-1"
          sandboxes={[snapshot("repo-ok", 0, 1500), snapshot("repo-fail", 1, 1200)]}
        />
      </EventFilterProvider>,
    );
    // Success repo: stdout area not rendered (collapsed by default).
    expect(screen.queryByTestId("sandbox-output-repo-ok")).not.toBeInTheDocument();
    // Failed repo: stdout area auto-expanded.
    expect(screen.getByTestId("sandbox-output-repo-fail")).toBeInTheDocument();
  });

  it("SandboxBox_CollapsedToggle_AdvertisesDurationInLabel", () => {
    render(
      <EventFilterProvider>
        <StepSandboxes runId="r-1" sandboxes={[snapshot("repo-ok", 0, 4500)]} />
      </EventFilterProvider>,
    );
    const toggle = screen.getByTestId("sandbox-toggle-repo-ok");
    expect(toggle).toHaveTextContent("expand");
    expect(toggle.textContent).toMatch(/4\.5s/);
  });

  it("SandboxBox_OnExpand_StdoutPlaceholderShowsDuration", () => {
    render(
      <EventFilterProvider>
        <StepSandboxes runId="r-1" sandboxes={[snapshot("repo-ok", 0, 4500)]} />
      </EventFilterProvider>,
    );
    fireEvent.click(screen.getByTestId("sandbox-toggle-repo-ok"));
    const output = screen.getByTestId("sandbox-output-repo-ok");
    expect(output.textContent).toMatch(/stdout hidden|step ran for 4\.5s/);
  });
});
