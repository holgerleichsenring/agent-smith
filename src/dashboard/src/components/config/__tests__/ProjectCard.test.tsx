import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { EntityCard } from "../EntityCard";
import type { ConfigCatalog } from "../useConfigCatalog";
import type { StudioProject, TemplateReference } from "@/lib/configApi";

// 2026-09-16-942a: the collapsed card, and the graph it expands into.
//
// A template belongs to a CONTEXT and a context belongs to a REPOSITORY. Drawn as one row
// of chips that shape is invisible — and two repositories declaring the same context name
// are one name, which is the configuration that silently dropped a template before
// 2026-09-16-4df5 separated them. The drawing reads nothing: every context in it comes
// from the project's own stored templates.

const template = (over: Partial<TemplateReference> = {}): TemplateReference => ({
  context: "server",
  contextRepo: "api",
  project: "refapp",
  repo: "api",
  templateContext: "server",
  ...over,
});

const project = (over: Partial<StudioProject> = {}): StudioProject => ({
  id: "sample",
  agent: "claude",
  tracker: "azdo",
  repos: ["api", "web"],
  pipeline: "",
  pipelines: [],
  resolution: { strategy: "tag", value: "sample" },
  defaultPipeline: "code",
  ...over,
});

const CATALOG: ConfigCatalog = {
  agents: [{ id: "claude", provider: "anthropic", models: {}, keySecret: null }],
  trackers: [{ id: "azdo", type: "azure", authSecret: "AZDO_PAT" }],
  connections: [],
  repos: [
    { id: "api", name: "api", branch: "main" },
    { id: "web", name: "web", branch: "main" },
  ],
  projects: [{ id: "refapp", agent: "claude", tracker: "azdo", repos: ["api"], pipeline: "", pipelines: [], resolution: null }],
  "mcp-servers": [],
  secrets: [],
} as unknown as ConfigCatalog;

function card(p: StudioProject, onEdit = () => {}) {
  return render(<EntityCard kind="projects" entity={p} catalog={CATALOG} onEdit={onEdit} />);
}

const expand = (id = "sample") => fireEvent.click(screen.getByTestId(`config-card-graph-toggle-${id}`));

beforeEach(() => vi.clearAllMocks());
afterEach(() => vi.unstubAllGlobals());

describe("ProjectCard wiring graph", () => {
  it("ProjectCard_Collapsed_NamesTheDefaultPipelineNotAPipelineCount", () => {
    // The card said "0 pipelines" for a project that runs perfectly well, because it
    // counted a list that decides nothing about what a ticket runs.
    card(project());

    const sub = screen.getByTestId("config-project-pipeline-sample");
    expect(sub.textContent).toBe("pipeline code");
    expect(sub.closest(".ec-sub")?.textContent).toContain("2 repositories");
    expect(sub.closest(".ec-sub")?.textContent).not.toContain("pipelines");
    expect(screen.queryByTestId("config-project-unresolved-sample")).toBeNull();
  });

  it("ProjectCard_Collapsed_NamesWhatIsUnresolved", () => {
    card(project({ agent: "gone", templates: [template({ templateContext: "" })] }));

    expect(screen.getByTestId("config-project-unresolved-sample").textContent).toContain(
      "2 unresolved",
    );
  });

  it("ProjectCard_Expanded_DrawsEachTemplatesContextUnderItsRepository", () => {
    card(project({ templates: [template(), template({ context: "client", contextRepo: "web", repo: "api" })] }));
    expand();

    // The context is drawn INSIDE its repository's block, and its template beside it.
    const apiBlock = screen.getByTestId("graph-block-sample-api");
    const webBlock = screen.getByTestId("graph-block-sample-web");
    expect(apiBlock.contains(screen.getByTestId("graph-node-repo-sample-api"))).toBe(true);
    expect(apiBlock.contains(screen.getByTestId("graph-node-context-sample-api-server-0"))).toBe(true);
    expect(webBlock.contains(screen.getByTestId("graph-node-context-sample-web-client-0"))).toBe(true);
    expect(apiBlock.contains(screen.getByTestId("graph-node-context-sample-web-client-0"))).toBe(false);
    expect(screen.getByTestId("graph-node-template-sample-api-server-0").textContent).toContain(
      "refapp / api · server",
    );
    // And the drawing states its own claim, for a reader who cannot see it.
    expect(screen.getByRole("img").getAttribute("aria-label")).toContain("2 repositories");
  });

  it("ProjectCard_TwoReposDeclaringOneContextName_AreTwoNodes", () => {
    // The case 4df5 exists for: one context NAME declared by two repositories.
    card(
      project({
        templates: [
          template({ context: "default", contextRepo: "api" }),
          template({ context: "default", contextRepo: "web" }),
        ],
      }),
    );
    expand();

    expect(screen.getByTestId("graph-node-context-sample-api-default-0")).toBeInTheDocument();
    expect(screen.getByTestId("graph-node-context-sample-web-default-0")).toBeInTheDocument();
  });

  it("ProjectCard_RepositoryNoTemplateNames_IsDrawnWithoutContexts", () => {
    card(project({ templates: [template()] }));
    expand();

    const webBlock = screen.getByTestId("graph-block-sample-web");
    expect(screen.getByTestId("graph-node-repo-sample-web")).toBeInTheDocument();
    // `web` is drawn, and nothing is invented under it.
    expect(webBlock.querySelectorAll('[data-testid^="graph-node-context-"]')).toHaveLength(0);
    // …while the repository a template DOES name carries its context.
    expect(
      screen.getByTestId("graph-block-sample-api").querySelectorAll('[data-testid^="graph-node-context-"]'),
    ).toHaveLength(1);
  });

  it("ProjectCard_UnfinishedTemplate_IsTheOnlyColouredMark", () => {
    card(project({ templates: [template(), template({ context: "client", contextRepo: "web", templateContext: "" })] }));
    expand();

    const coloured = [...document.querySelectorAll('[data-coloured="true"]')].map((n) =>
      n.getAttribute("data-testid"),
    );
    expect(coloured).toEqual([
      "graph-node-context-sample-web-client-0",
      "graph-node-template-sample-web-client-0",
    ]);
  });

  it("ProjectCard_Expanding_DoesNotOpenTheEditor", () => {
    // The whole card is the edit button, so the disclosure control must stop the event.
    const onEdit = vi.fn();
    card(project({ templates: [template()] }), onEdit);

    expand();
    expect(onEdit).not.toHaveBeenCalled();
    expect(screen.getByTestId("config-card-graph-sample")).toBeInTheDocument();
    // Clicking inside the drawing does not open it either.
    fireEvent.click(screen.getByTestId("graph-node-repo-sample-api"));
    expect(onEdit).not.toHaveBeenCalled();
    // The card itself still opens the editor.
    fireEvent.click(screen.getByTestId("config-card-edit-sample"));
    expect(onEdit).toHaveBeenCalledTimes(1);
    // And expanding lifts the card's clipping, which would cut the drawing off.
    expect(screen.getByTestId("config-card-projects-sample")).toHaveAttribute("data-expanded", "true");
  });

  it("ProjectCard_Expanded_MakesNoOutboundCall", () => {
    // Asking the contexts endpoint would be one authenticated call per repository per
    // card, each re-assembling the whole configuration, on a page listing every project.
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    card(project({ templates: [template(), template({ context: "client", contextRepo: "web" })] }));

    expand();

    expect(screen.getByTestId("config-card-graph-sample")).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });
});
