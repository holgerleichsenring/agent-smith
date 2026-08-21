import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { EntityCard } from "../EntityCard";
import type { ConfigCatalog } from "../useConfigCatalog";
import type { StudioProject } from "@/lib/configApi";

// p0489: the Initialize action on the PROJECT card. Starting an init writes no
// ticket anywhere; the card's only job is to hand the operator the run it started
// — or to say, right here, why it could not start, and stay pressable.

const PROJECT: StudioProject = {
  id: "sample",
  agent: "claude",
  tracker: "azdo",
  repos: ["sample-server"],
  pipeline: "feature",
  pipelines: ["feature"],
  resolution: null,
};

const CATALOG: ConfigCatalog = {
  agents: [{ id: "claude", provider: "anthropic", models: {}, keySecret: null }],
  trackers: [{ id: "azdo", type: "azure", authSecret: "AZDO_PAT" }],
  connections: [],
  repos: [{ id: "sample-server", name: "sample-server", branch: "main" }],
  projects: [PROJECT],
  "mcp-servers": [],
  secrets: [],
};

function renderCard() {
  return render(
    <EntityCard kind="projects" entity={PROJECT} catalog={CATALOG} onEdit={() => {}} />,
  );
}

function respond(status: number, body: unknown) {
  return vi.fn().mockResolvedValue({
    ok: status >= 200 && status < 300,
    status,
    json: async () => body,
  } as unknown as Response);
}

function bodyOf(fetchMock: ReturnType<typeof vi.fn>): unknown {
  const init = fetchMock.mock.calls[0][1] as RequestInit;
  return JSON.parse(init.body as string);
}

beforeEach(() => vi.clearAllMocks());
afterEach(() => vi.unstubAllGlobals());

describe("ProjectCard init action", () => {
  it("ProjectCard_InitializeAction_PostsAndLinksToTheRun", async () => {
    const fetchMock = respond(200, { runId: "2026-08-20T09-00-00-abcd", reason: null });
    vi.stubGlobal("fetch", fetchMock);
    renderCard();

    fireEvent.click(screen.getByTestId("project-init-sample"));

    const link = await screen.findByTestId("project-init-running-sample");
    expect(link).toHaveAttribute("href", "/jobs/2026-08-20T09-00-00-abcd");
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/projects/sample/init",
      expect.objectContaining({ method: "POST" }),
    );
  });

  it("InitializeAction_AutoAcceptCheckbox_DefaultsToOn_AndTravelsWithTheLaunch", async () => {
    const fetchMock = respond(200, { runId: "2026-08-20T09-00-00-abcd", reason: null });
    vi.stubGlobal("fetch", fetchMock);
    renderCard();

    const checkbox = screen.getByTestId("project-init-auto-accept-sample");
    // Nobody reviews an init PR, so the honest default is the tick already set.
    expect(checkbox).toBeChecked();

    fireEvent.click(screen.getByTestId("project-init-sample"));

    await screen.findByTestId("project-init-running-sample");
    expect(bodyOf(fetchMock)).toEqual({ autoCompletePullRequests: true });
  });

  it("InitializeAction_AutoAcceptUnticked_LaunchesWithoutIt", async () => {
    const fetchMock = respond(200, { runId: "2026-08-20T09-00-00-abcd", reason: null });
    vi.stubGlobal("fetch", fetchMock);
    renderCard();

    fireEvent.click(screen.getByTestId("project-init-auto-accept-sample"));
    fireEvent.click(screen.getByTestId("project-init-sample"));

    await screen.findByTestId("project-init-running-sample");
    // The consent is per launch — untick it and this run merges nothing.
    expect(bodyOf(fetchMock)).toEqual({ autoCompletePullRequests: false });
  });

  it("ProjectCard_Refusal_RendersInline_AndTheActionStaysPressable", async () => {
    const fetchMock = respond(503, { runId: null, reason: "no capacity — footprint 4Gi / 1 cpu exceeds the remaining budget" });
    vi.stubGlobal("fetch", fetchMock);
    renderCard();

    fireEvent.click(screen.getByTestId("project-init-sample"));

    const refusal = await screen.findByTestId("project-init-refusal-sample");
    expect(refusal).toHaveTextContent("exceeds the remaining budget");
    // Pressing again IS the re-run — the action must not latch itself off.
    const button = screen.getByTestId("project-init-sample");
    expect(button).toBeEnabled();
    fireEvent.click(button);
    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2));
  });

  it("ProjectCard_WhileAnInitRuns_ShowsItRunning_AndLinksToIt", async () => {
    const fetchMock = respond(409, {
      runId: "2026-08-20T08-00-00-beef",
      reason: "An initialization is already running (run 2026-08-20T08-00-00-beef).",
    });
    vi.stubGlobal("fetch", fetchMock);
    renderCard();

    fireEvent.click(screen.getByTestId("project-init-sample"));

    const link = await screen.findByTestId("project-init-running-sample");
    expect(link).toHaveTextContent("Initializing");
    expect(link).toHaveAttribute("href", "/jobs/2026-08-20T08-00-00-beef");
    // The live run replaces the button — a second click opens it, never a second init.
    expect(screen.queryByTestId("project-init-sample")).not.toBeInTheDocument();
  });
});
