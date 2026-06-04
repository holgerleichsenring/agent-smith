import { render, screen } from "@testing-library/react";
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

  it("CatalogLoadBody_LinksToCatalogBrowser_InsteadOfRawLists", () => {
    // p0221: the per-run step links to the system catalog browser rather than
    // duplicating the skill/master/concept names inline.
    render(<CatalogLoadBody events={[catalogEvent()]} />);

    expect(screen.getByTestId("catalog-load-body-browse")).toHaveAttribute("href", "/system/catalog");
    expect(screen.queryByTestId("catalog-load-body-skills-toggle")).not.toBeInTheDocument();
    expect(screen.queryByTestId("catalog-load-body-concepts-toggle")).not.toBeInTheDocument();
  });
});
