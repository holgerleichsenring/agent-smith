import { describe, it, expect, vi, beforeEach } from "vitest";
import { useState } from "react";
import { render, screen, fireEvent } from "@testing-library/react";
import { EntityForm } from "../EntityForm";
import type { ConfigCatalog } from "../useConfigCatalog";
import type { ConfigCapabilities, StudioProject } from "@/lib/configApi";
import { fetchConnectionRepos } from "@/lib/configApi";

// p0345c: the repo picker talks to the discovery cache — mock only that call,
// the rest of the module (types, entities' CRUD clients) stays real.
vi.mock("@/lib/configApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/configApi")>()),
  fetchConnectionRepos: vi.fn(),
}));

const mockedRepos = vi.mocked(fetchConnectionRepos);

const catalog: ConfigCatalog = {
  agents: [{ id: "gpt5", provider: "openai", models: { coding: { model: "c" }, scan: { model: "s" } }, keySecret: "K" }],
  trackers: [{ id: "azdo", type: "azure", organization: "o", project: "p", authSecret: "T" }],
  connections: [
    { id: "conn", type: "azure-devops", organization: "acme", project: "core", authSecret: "T", defaultBranch: "main" },
  ],
  repos: [
    { id: "web", name: "web", branch: "main" },
    { id: "api", name: "api", branch: "main" },
  ],
  projects: [],
  "mcp-servers": [],
  secrets: [{ id: "K" }, { id: "T" }],
};

const capabilities: ConfigCapabilities = {
  trackerTypes: [],
  connectionTypes: [],
  agentProviders: ["azure-openai"],
  resolutionStrategies: ["tag", "area_path", "repo", "to_address"],
  pipelines: ["feature-implementation", "api-scan"],
};

function Harness() {
  const [draft, setDraft] = useState<StudioProject>({
    id: "proj",
    agent: "",
    tracker: "",
    repos: [],
    pipeline: "",
    pipelines: [],
    resolution: null,
  });
  return (
    <EntityForm
      kind="projects"
      draft={draft}
      onChange={(n) => setDraft(n as StudioProject)}
      catalog={catalog}
      capabilities={capabilities}
      isNew
    />
  );
}

beforeEach(() => {
  mockedRepos.mockReset();
  mockedRepos.mockResolvedValue({
    discoveredAt: "2026-07-17T09:00:00Z",
    repos: [
      { name: "Sample.Api", defaultBranch: "main" },
      { name: "Sample.Web", defaultBranch: null },
    ],
  });
});

describe("ProjectForm", () => {
  it("ProjectForm_RefsPickedFromCatalog_NeverFreeText", () => {
    render(<Harness />);
    // agent + tracker refs are <select>s (pick-only), never text inputs.
    const agent = screen.getByTestId("form-ref-agent");
    const tracker = screen.getByTestId("form-ref-tracker");
    expect(agent.tagName).toBe("SELECT");
    expect(tracker.tagName).toBe("SELECT");
    // The options come straight from the catalog.
    expect(agent.querySelector('option[value="gpt5"]')).not.toBeNull();
    expect(tracker.querySelector('option[value="azdo"]')).not.toBeNull();
    // Repos are pick-only toggle chips, one per catalog repo — no text entry.
    expect(screen.getByTestId("form-ref-repos-option-web")).toBeInTheDocument();
    expect(screen.getByTestId("form-ref-repos-option-api")).toBeInTheDocument();
  });

  it("ProjectForm_IntegrityFlipsGreen_WhenAllRefsResolve", () => {
    render(<Harness />);
    // Starts unresolved (no refs picked yet).
    expect(screen.getByTestId("project-integrity")).toHaveAttribute("data-ok", "false");

    fireEvent.change(screen.getByTestId("form-ref-agent"), { target: { value: "gpt5" } });
    fireEvent.change(screen.getByTestId("form-ref-tracker"), { target: { value: "azdo" } });
    // Still amber until at least one repo is chosen.
    expect(screen.getByTestId("project-integrity")).toHaveAttribute("data-ok", "false");

    fireEvent.click(screen.getByTestId("form-ref-repos-option-web"));

    expect(screen.getByTestId("project-integrity")).toHaveAttribute("data-ok", "true");
    // p0343c: the mock's integrity copy
    expect(screen.getByTestId("project-integrity")).toHaveTextContent("Every reference resolves");
  });

  it("RepoPicker_OffersDiscoveredRepos_AndWildcard", async () => {
    // p0345c: picking a connection loads its discovery cache — the repos that
    // ACTUALLY exist there are offered as toggle chips, and a wildcard/glob
    // stays possible next to them.
    render(<Harness />);
    fireEvent.change(screen.getByTestId("form-connref-connection"), { target: { value: "conn" } });

    // The discovered repos render as pick chips.
    const api = await screen.findByTestId("form-connref-discovered-Sample.Api");
    expect(screen.getByTestId("form-connref-discovered-Sample.Web")).toBeInTheDocument();
    expect(mockedRepos).toHaveBeenCalledWith("conn", expect.anything());

    // Toggling a discovered repo adds the conn-scoped ref chip…
    fireEvent.click(api);
    expect(api).toHaveAttribute("data-selected", "true");
    expect(screen.getByTestId("form-connref-chip-conn/Sample.Api")).toBeInTheDocument();
    // …and toggling again removes it.
    fireEvent.click(screen.getByTestId("form-connref-discovered-Sample.Api"));
    expect(screen.queryByTestId("form-connref-chip-conn/Sample.Api")).toBeNull();

    // The wildcard path stays: a glob is typed, not picked.
    fireEvent.change(screen.getByTestId("form-connref-name"), { target: { value: "*" } });
    fireEvent.click(screen.getByTestId("form-connref-add"));
    expect(screen.getByTestId("form-connref-chip-conn/*")).toBeInTheDocument();
    expect(screen.getByTestId("wiring-repo-conn/*")).toHaveAttribute("data-resolved", "true");
  });

  it("RepoPicker_NotDiscoveredYet_HonestState_FreeTextStillWorks", async () => {
    // discoveredAt null = the discovery never ran — the picker says so and
    // falls back to typing a name instead of pretending an empty inventory.
    mockedRepos.mockResolvedValue({ discoveredAt: null, repos: [] });
    render(<Harness />);
    fireEvent.change(screen.getByTestId("form-connref-connection"), { target: { value: "conn" } });

    const honest = await screen.findByTestId("form-connref-undiscovered");
    expect(honest).toHaveTextContent("not discovered yet — run a discovery or type a name");

    fireEvent.change(screen.getByTestId("form-connref-name"), { target: { value: "Sample.Api" } });
    fireEvent.click(screen.getByTestId("form-connref-add"));
    expect(screen.getByTestId("form-connref-chip-conn/Sample.Api")).toBeInTheDocument();
  });

  it("ProjectForm_ResolutionStrategySelector_FromCapabilities", () => {
    // p0345c: resolution is a strategy CHOICE from the backend's registry plus
    // a value with a per-strategy hint — no freetext guessing.
    render(<Harness />);
    const strategy = screen.getByTestId("form-field-resolution-strategy");
    expect(strategy.tagName).toBe("SELECT");
    for (const s of capabilities.resolutionStrategies) {
      expect(strategy.querySelector(`option[value="${s}"]`), `strategy ${s}`).not.toBeNull();
    }
    // No strategy → no value input (resolution stays null).
    expect(screen.queryByTestId("form-field-resolution-value")).toBeNull();

    fireEvent.change(strategy, { target: { value: "tag" } });
    const value = screen.getByTestId("form-field-resolution-value");
    expect(value).toHaveAttribute("placeholder", "e.g. Rheview");

    fireEvent.change(value, { target: { value: "Rheview" } });
    expect(screen.getByTestId("form-field-resolution-value")).toHaveValue("Rheview");

    // Switching the strategy switches the hint.
    fireEvent.change(screen.getByTestId("form-field-resolution-strategy"), { target: { value: "to_address" } });
    expect(screen.getByTestId("form-field-resolution-value")).toHaveAttribute(
      "placeholder",
      "e.g. team@example.com",
    );
  });

  it("ProjectForm_PipelineSelect_FromCapabilities", () => {
    // The field once mislabeled "trigger" is a pipeline SELECT now.
    render(<Harness />);
    const pipeline = screen.getByTestId("form-field-pipeline");
    expect(pipeline.tagName).toBe("SELECT");
    expect(pipeline.querySelector('option[value="feature-implementation"]')).not.toBeNull();
    expect(pipeline.querySelector('option[value="api-scan"]')).not.toBeNull();
    fireEvent.change(pipeline, { target: { value: "api-scan" } });
    expect(screen.getByTestId("form-field-pipeline")).toHaveValue("api-scan");
  });

  it("ProjectForm_ConnScopedRepoRef_AddedViaConnectionPicker_CountsForIntegrity", async () => {
    // p0345b: the operator-shaped config references repos through a
    // connection ("conn/Name") — added via the connection picker + repo-name
    // input, and integrity treats the resolved connection as a valid ref.
    render(<Harness />);
    fireEvent.change(screen.getByTestId("form-ref-agent"), { target: { value: "gpt5" } });
    fireEvent.change(screen.getByTestId("form-ref-tracker"), { target: { value: "azdo" } });

    fireEvent.change(screen.getByTestId("form-connref-connection"), { target: { value: "conn" } });
    await screen.findByTestId("form-connref-discovered-Sample.Api");
    fireEvent.change(screen.getByTestId("form-connref-name"), { target: { value: "Sample.Api" } });
    fireEvent.click(screen.getByTestId("form-connref-add"));

    expect(screen.getByTestId("form-connref-chip-conn/Sample.Api")).toBeInTheDocument();
    expect(screen.getByTestId("wiring-repo-conn/Sample.Api")).toHaveAttribute("data-resolved", "true");
    expect(screen.getByTestId("project-integrity")).toHaveAttribute("data-ok", "true");

    // Removing the conn-scoped ref drops integrity back to amber.
    fireEvent.click(screen.getByTestId("form-connref-remove-conn/Sample.Api"));
    expect(screen.getByTestId("project-integrity")).toHaveAttribute("data-ok", "false");
  });

  it("ProjectForm_UnknownRepoRemoved_IntegrityReturnsAmber", () => {
    render(<Harness />);
    fireEvent.change(screen.getByTestId("form-ref-agent"), { target: { value: "gpt5" } });
    fireEvent.change(screen.getByTestId("form-ref-tracker"), { target: { value: "azdo" } });
    fireEvent.click(screen.getByTestId("form-ref-repos-option-web"));
    expect(screen.getByTestId("project-integrity")).toHaveAttribute("data-ok", "true");
    // Deselecting the only repo drops integrity back to amber.
    fireEvent.click(screen.getByTestId("form-ref-repos-option-web"));
    expect(screen.getByTestId("project-integrity")).toHaveAttribute("data-ok", "false");
  });
});
