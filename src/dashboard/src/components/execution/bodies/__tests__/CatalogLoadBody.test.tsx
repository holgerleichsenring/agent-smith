import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { CatalogLoadBody } from "../CatalogLoadBody";
import { EventType, type RunEvent } from "@/types/hub-events";

function catalogEvent(over: Partial<Extract<RunEvent, { type: EventType.CatalogLoaded }>> = {}): RunEvent {
  return {
    type: EventType.CatalogLoaded,
    runId: "run-1",
    timestamp: "2026-06-03T15:09:55.000Z",
    version: "v3.7.0",
    source: "Default",
    sourceUrl: "https://github.com/agentsmith-skills/releases/v3.7.0",
    conceptCount: 74,
    skillsLoaded: 19,
    mastersCount: 6,
    fromCache: true,
    durationMs: 12,
    skillNames: ["auth-reviewer", "coding-planner"],
    masterNames: ["coding-agent-master"],
    conceptNames: ["api_changed", "auth_present", "schema_breaking"],
    ...over,
  };
}

describe("CatalogLoadBody", () => {
  it("CatalogLoadBody_RendersVersionSourceUrl_AndCountsLine", () => {
    render(<CatalogLoadBody events={[catalogEvent()]} />);

    expect(screen.getByTestId("catalog-load-body-version")).toHaveTextContent("v3.7.0");
    expect(screen.getByTestId("catalog-load-body-source")).toHaveTextContent("default");
    expect(screen.getByTestId("catalog-load-body-url")).toHaveTextContent("v3.7.0");
    expect(screen.getByTestId("catalog-load-body-counts")).toHaveTextContent(
      "74 concepts · 19 skills · 6 masters",
    );
    expect(screen.getByTestId("catalog-load-body-loaded-at")).toHaveTextContent("15:09:55");
  });

  it("CatalogLoadBody_FromCache_ShowsCacheBadge_ElseFresh", () => {
    const { rerender } = render(<CatalogLoadBody events={[catalogEvent({ fromCache: true })]} />);
    expect(screen.getByTestId("catalog-load-body-cache")).toHaveTextContent("warm cache");
    expect(screen.queryByTestId("catalog-load-body-fresh")).not.toBeInTheDocument();

    rerender(<CatalogLoadBody events={[catalogEvent({ fromCache: false })]} />);
    expect(screen.getByTestId("catalog-load-body-fresh")).toHaveTextContent("fresh pull");
    expect(screen.queryByTestId("catalog-load-body-cache")).not.toBeInTheDocument();
  });

  it("CatalogLoadBody_NoEvent_ShowsWaiting", () => {
    render(<CatalogLoadBody events={[]} />);
    expect(screen.getByTestId("catalog-load-body")).toHaveTextContent("Waiting for catalog binding");
  });

  it("CatalogLoadBody_ExpandSkills_ListsSkillNames", () => {
    render(<CatalogLoadBody events={[catalogEvent()]} />);

    expect(screen.queryByTestId("catalog-load-body-skills-items")).not.toBeInTheDocument();
    fireEvent.click(screen.getByTestId("catalog-load-body-skills-toggle"));

    const list = screen.getByTestId("catalog-load-body-skills-items");
    expect(list).toHaveTextContent("auth-reviewer");
    expect(list).toHaveTextContent("coding-planner");
  });

  it("CatalogLoadBody_RendersMasterAndConceptLists_WithCounts", () => {
    render(<CatalogLoadBody events={[catalogEvent()]} />);

    expect(screen.getByTestId("catalog-load-body-masters-toggle")).toHaveTextContent("Masters");
    expect(screen.getByTestId("catalog-load-body-masters-toggle")).toHaveTextContent("1");
    expect(screen.getByTestId("catalog-load-body-concepts-toggle")).toHaveTextContent("Concepts");
    expect(screen.getByTestId("catalog-load-body-concepts-toggle")).toHaveTextContent("3");
  });

  it("CatalogLoadBody_ConceptFilter_NarrowsConceptList", () => {
    render(<CatalogLoadBody events={[catalogEvent()]} />);

    fireEvent.click(screen.getByTestId("catalog-load-body-concepts-toggle"));
    let list = screen.getByTestId("catalog-load-body-concepts-items");
    expect(list).toHaveTextContent("api_changed");
    expect(list).toHaveTextContent("auth_present");
    expect(list).toHaveTextContent("schema_breaking");

    fireEvent.change(screen.getByTestId("catalog-load-body-concepts-filter"), { target: { value: "auth" } });
    list = screen.getByTestId("catalog-load-body-concepts-items");
    expect(list).toHaveTextContent("auth_present");
    expect(list).not.toHaveTextContent("schema_breaking");
    expect(list).not.toHaveTextContent("api_changed");
  });

  it("CatalogLoadBody_MissingNameArrays_FallsBackToCounts", () => {
    render(
      <CatalogLoadBody
        events={[catalogEvent({ skillNames: undefined, masterNames: undefined, conceptNames: undefined })]}
      />,
    );

    expect(screen.getByTestId("catalog-load-body-counts")).toHaveTextContent(
      "74 concepts · 19 skills · 6 masters",
    );
    expect(screen.queryByTestId("catalog-load-body-skills")).not.toBeInTheDocument();
    expect(screen.queryByTestId("catalog-load-body-masters")).not.toBeInTheDocument();
    expect(screen.queryByTestId("catalog-load-body-concepts")).not.toBeInTheDocument();
  });
});
