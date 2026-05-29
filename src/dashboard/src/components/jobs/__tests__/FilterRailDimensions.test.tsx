import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { FilterRail } from "../FilterRail";
import { EventFilterProvider } from "@/lib/EventFilterContext";

const replaceMock = vi.fn();
let searchParamsValue = new URLSearchParams();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: replaceMock }),
  usePathname: () => "/jobs/run-1",
  useSearchParams: () => searchParamsValue,
}));

beforeEach(() => {
  replaceMock.mockClear();
  searchParamsValue = new URLSearchParams();
});

describe("FilterRail dimensions", () => {
  it("FilterRail_DimensionFilter_TogglingAgentRestrictsTrailToMatching", () => {
    render(
      <EventFilterProvider>
        <FilterRail observedDimensions={{ agent: ["UploadAuditor", "PathTrace"] }} />
      </EventFilterProvider>,
    );

    const chip = screen.getByTestId("filter-chip-agent-UploadAuditor");
    expect(chip).toHaveAttribute("aria-pressed", "false");

    fireEvent.click(chip);

    expect(replaceMock).toHaveBeenCalled();
    const url = replaceMock.mock.calls[0][0] as string;
    expect(url).toContain("d.agent=UploadAuditor");
  });

  it("FilterRail_DimensionFilter_QueryStringPersistsAcrossReload", () => {
    searchParamsValue = new URLSearchParams("d.agent=UploadAuditor,PathTrace");
    render(
      <EventFilterProvider>
        <FilterRail observedDimensions={{ agent: ["UploadAuditor", "PathTrace"] }} />
      </EventFilterProvider>,
    );

    expect(screen.getByTestId("filter-chip-agent-UploadAuditor"))
      .toHaveAttribute("aria-pressed", "true");
    expect(screen.getByTestId("filter-chip-agent-PathTrace"))
      .toHaveAttribute("aria-pressed", "true");
  });

  it("FilterRail_DimensionFilter_SandboxAndPipelineCompose", () => {
    render(
      <EventFilterProvider>
        <FilterRail
          observedDimensions={{
            sandbox: ["api-repo"],
            pipeline: ["fix-bug"],
          }}
        />
      </EventFilterProvider>,
    );

    fireEvent.click(screen.getByTestId("filter-chip-sandbox-api-repo"));
    fireEvent.click(screen.getByTestId("filter-chip-pipeline-fix-bug"));

    expect(replaceMock).toHaveBeenCalledTimes(2);
    const finalUrl = replaceMock.mock.calls[1][0] as string;
    expect(finalUrl).toContain("d.sandbox=api-repo");
    expect(finalUrl).toContain("d.pipeline=fix-bug");
  });

  it("renders empty-state when no observed values arrive", () => {
    render(
      <EventFilterProvider>
        <FilterRail observedDimensions={{}} />
      </EventFilterProvider>,
    );
    expect(screen.getByTestId("filter-dim-agent-empty")).toBeInTheDocument();
  });
});
