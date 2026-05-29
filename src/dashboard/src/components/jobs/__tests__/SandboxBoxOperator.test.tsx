import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { SandboxBox } from "../SandboxBox";
import { EventFilterProvider } from "@/lib/EventFilterContext";

vi.mock("@/hooks/useSandboxEvents", () => ({
  useSandboxEvents: () => ({ command: null, outputs: [] }),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: vi.fn() }),
  usePathname: () => "/jobs/run-1",
  useSearchParams: () => new URLSearchParams(),
}));

describe("SandboxBox sub-agent attribution", () => {
  it("SandboxBox_DisplaysOperatingSubAgentName", () => {
    render(
      <EventFilterProvider>
        <SandboxBox
          runId="r-1"
          repo="api-repo"
          expanded={false}
          onToggle={() => {}}
          operatingSubAgentName="UploadHandlerAuditor"
        />
      </EventFilterProvider>,
    );

    expect(screen.getByTestId("sandbox-operator-api-repo"))
      .toHaveTextContent("UploadHandlerAuditor");
  });

  it("does not show the operator badge when no sub-agent has touched the sandbox", () => {
    render(
      <EventFilterProvider>
        <SandboxBox runId="r-1" repo="api-repo" expanded={false} onToggle={() => {}} />
      </EventFilterProvider>,
    );

    expect(screen.queryByTestId("sandbox-operator-api-repo")).not.toBeInTheDocument();
  });
});
