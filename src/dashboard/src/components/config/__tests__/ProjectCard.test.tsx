import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { readFileSync } from "node:fs";
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

// 2026-09-16-d7c3: the ROW is the disclosure. The separate control labelled "wiring" is gone.
const expand = (id = "sample") => fireEvent.click(screen.getByTestId(`config-card-disclosure-${id}`));

beforeEach(() => vi.clearAllMocks());
afterEach(() => vi.unstubAllGlobals());

describe("ProjectCard wiring graph", () => {
  it("ProjectCard_Collapsed_StatesEachFactSeparately", () => {
    // The card said "0 pipelines" for a project that runs perfectly well, because it counted
    // a list that decides nothing about what a ticket runs. 2026-09-16-bedc: and it said the
    // rest as one run-on sentence, so no fact could be found without reading the others.
    card(project());

    expect(screen.getByTestId("config-project-pipeline-sample").textContent).toBe("default · code");
    expect(screen.getByTestId("config-project-repos-sample").textContent).toBe("2 repositories");
    expect(screen.getByTestId("config-project-resolution-sample")).toBeInTheDocument();
    expect(screen.getByTestId("config-project-pipeline-sample").closest(".ec-sub")?.textContent)
      .not.toContain("pipelines");
    expect(screen.queryByTestId("config-project-unresolved-sample")).toBeNull();
    expect(screen.queryByTestId("config-project-unfinished-sample")).toBeNull();
  });

  it("ProjectCard_Collapsed_DrawsNoChipRow", () => {
    // The graph behind the card's own control IS the wiring. The flat chip row was a second
    // copy of it, left in place because 942a described an addition where a replacement
    // was meant.
    const { container } = card(project());

    expect(screen.queryByTestId("config-card-wiring-sample")).toBeNull();
    expect(container.querySelector(".wire")).toBeNull();
  });

  it("ProjectCard_UnfinishedTemplate_IsNamedApartFromUnresolvedReferences", () => {
    // Two problems with two different fixes: a reference names a catalog entry that does not
    // exist; an unfinished binding is one nobody finished typing. One number named neither.
    card(project({ agent: "gone", templates: [template({ templateContext: "" })] }));

    expect(screen.getByTestId("config-project-unresolved-sample").textContent)
      .toContain("1 unresolved");
    expect(screen.getByTestId("config-project-unfinished-sample").textContent)
      .toContain("1 template unfinished");
  });

  // 2026-09-16-4b41: the four tests that stood here read the drawing's BLOCKS — a context
  // nested inside its repository's rectangle. Five columns retires that shape, and the card
  // test file was already half graph, so what they proved moved to ProjectGraph.test.tsx.

  it("ProjectCard_ClickingTheRow_ExpandsAndDoesNotOpenTheEditor", () => {
    // 2026-09-16-d7c3: the row is the disclosure and Edit is the only route into the editor.
    // It was the other way round — the whole card opened the editor and the disclosure hid
    // behind a control labelled "wiring".
    const onEdit = vi.fn();
    const { container } = card(project({ templates: [template()] }), onEdit);

    expand();
    expect(onEdit).not.toHaveBeenCalled();
    expect(screen.getByTestId("config-card-graph-sample")).toBeInTheDocument();
    // Clicking inside the drawing does not open it either.
    fireEvent.click(screen.getByTestId("graph-node-repo-sample-api"));
    expect(onEdit).not.toHaveBeenCalled();
    // Nor does clicking the card body outside any control.
    fireEvent.click(screen.getByTestId("config-card-projects-sample"));
    expect(onEdit).not.toHaveBeenCalled();
    // Only Edit does.
    fireEvent.click(screen.getByTestId("config-card-edit-sample"));
    expect(onEdit).toHaveBeenCalledTimes(1);

    // The row says what it controls, and it names the panel it opened.
    const row = screen.getByTestId("config-card-disclosure-sample");
    expect(row.getAttribute("aria-expanded")).toBe("true");
    const panel = container.querySelector(`#${row.getAttribute("aria-controls")}`);
    expect(panel, "aria-controls names the panel that appeared").not.toBeNull();
    expect(panel!.querySelector('[data-testid="config-card-graph-sample"]')).not.toBeNull();
    // And expanding lifts the card's clipping, which would cut the drawing off.
    expect(screen.getByTestId("config-card-projects-sample")).toHaveAttribute("data-expanded", "true");
  });

  it("ProjectCard_IdAndMarksShareOneFlexRow", () => {
    // jsdom does no layout; what it CAN pin is the class the stylesheet keys the row on, and
    // that the marks are inside it rather than under it.
    const { container } = card(project());

    const row = container.querySelector(".ec-id.row");
    expect(row, "the id block is the row when the card carries marks").not.toBeNull();
    expect(row!.querySelector(".ec-name")!.textContent).toBe("sample");
    expect(row!.querySelector(".ec-sub.ec-marks")).not.toBeNull();

    const css = readFileSync("src/styles/mock-parity.css", "utf8");
    expect(css).toContain(".mock-config .ecard .ec-id { min-width: 0;");
    expect(css).toMatch(/\.mock-config \.ecard \.ec-id\.row \{[^}]*display: flex;/);
  });

  it("ProjectCard_NoSeparateWiringControl", () => {
    // The control an operator would never click is gone: the row it sat beside does the job.
    const { container } = card(project());

    expect(screen.queryByTestId("config-card-graph-toggle-sample")).toBeNull();
    expect(container.textContent).not.toContain("Wiring");
    const expanders = [...container.querySelectorAll("[aria-expanded]")];
    expect(expanders.map((n) => n.getAttribute("data-testid")))
      .toEqual(["config-card-disclosure-sample"]);
  });

  it("ProjectCard_Row_IsNotARoleButtonHostingButtons", () => {
    // An ARIA button takes no interactive descendants. The card root carried role=button and
    // contained the init control, its checkbox label and the edit control — invalid today,
    // and the reason the row is what became the button.
    const { container } = card(project());

    expect(container.querySelectorAll('[role="button"]')).toHaveLength(0);
    const row = screen.getByTestId("config-card-disclosure-sample");
    expect(row.tagName).toBe("BUTTON");
    expect(row.querySelectorAll("button, input, label, a")).toHaveLength(0);
    // The controls are the row's SIBLING, not its children.
    expect(row.querySelector(".ec-right")).toBeNull();
    expect(container.querySelector(".ec-top > .ec-right")).not.toBeNull();
  });

  it("ProjectCard_EnterOnEdit_OpensTheEditorExactlyOnce", () => {
    // The root's Enter handler had no target guard, so Enter on a descendant control fired
    // the control AND the editor. A browser answers Enter on a <button> with a click; the
    // key press and the click together must open the editor once.
    const onEdit = vi.fn();
    card(project(), onEdit);

    const edit = screen.getByTestId("config-card-edit-sample");
    fireEvent.keyDown(edit, { key: "Enter" });
    fireEvent.click(edit);
    expect(onEdit).toHaveBeenCalledTimes(1);

    // And Enter on the row expands rather than editing.
    const row = screen.getByTestId("config-card-disclosure-sample");
    fireEvent.keyDown(row, { key: "Enter" });
    fireEvent.click(row);
    expect(onEdit).toHaveBeenCalledTimes(1);
    expect(row.getAttribute("aria-expanded")).toBe("true");
  });

  it("ProjectCard_MatchesTheReferenceFile", () => {
    // src/dashboard/design/mockups/project-card.html is the SUBJECT of this phase, not an
    // illustration of it. Only what it marks CHANGES is normative — the rest is the running
    // app copied so the page renders — so this reads the changed block and asserts every
    // class it names is produced by the card.
    const html = readFileSync("design/mockups/project-card.html", "utf8");
    const marker = "================= CHANGES =================";
    expect(html, "the reference still marks what is normative").toContain(marker);
    const changes = html.slice(html.indexOf(marker), html.indexOf("</style>"));
    const rules = changes.replace(/\/\*[\s\S]*?\*\//g, "");
    const named = new Set<string>();
    for (const rule of rules.split("}")) {
      const selector = rule.split("{")[0];
      if (!rule.includes("{")) continue;
      for (const m of selector.matchAll(/\.([a-z][a-z0-9-]*)/g)) named.add(m[1]);
    }
    expect(named.size, "the changed block names classes").toBeGreaterThan(4);

    const { container } = card(project({ templates: [template()] }));
    expand();
    for (const cls of named) {
      expect(container.querySelector(`.${cls}`), `the card produces .${cls}`).not.toBeNull();
    }
    // And the reference's own markup contract: the row is a button carrying both aria hooks.
    expect(changes + html).toMatch(/class="ec-disclosure"[\s\S]*?aria-expanded[\s\S]*?aria-controls/);
  });

  it("OtherCards_StillOpenTheEditorWhenClicked", () => {
    // The row treatment is conditional. The other six kinds keep the whole-card click and the
    // prose sub-line — a secret's is a full sentence, which must not become a chip row.
    const onEdit = vi.fn();
    const { container } = render(
      <EntityCard
        kind="secrets"
        entity={{ id: "AZDO_PAT" } as unknown as StudioProject}
        catalog={CATALOG}
        onEdit={onEdit}
      />,
    );

    const root = screen.getByTestId("config-card-secrets-AZDO_PAT");
    expect(root.getAttribute("role")).toBe("button");
    expect(container.querySelector(".ec-disclosure")).toBeNull();
    expect(container.querySelector(".ec-id.row")).toBeNull();
    expect(screen.getByTestId("config-secret-redaction-AZDO_PAT").textContent)
      .toContain("value resolved from runtime, never stored here");
    expect(container.querySelector(".edit-hint")).not.toBeNull();

    fireEvent.click(root);
    expect(onEdit).toHaveBeenCalledTimes(1);

    // …and the guard the root never had: Enter on a descendant is not Enter on the card.
    fireEvent.keyDown(screen.getByTestId("config-card-edit-AZDO_PAT"), { key: "Enter" });
    expect(onEdit).toHaveBeenCalledTimes(1);
    fireEvent.keyDown(root, { key: "Enter" });
    expect(onEdit).toHaveBeenCalledTimes(2);
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

  it("ProjectCard_ControlsAndSummary_KeepTheirLayoutHooks", () => {
    // jsdom does no layout, so nothing here can see that the buttons wrap. What it CAN pin is
    // the contract the stylesheet keys on: the summary became a row of marks whose min-content
    // width is the widest mark, so the middle block must yield and the controls must not
    // shrink. Dropping either class silently reproduces the defect.
    const { container } = card(project());

    expect(container.querySelector(".ec-id"), "the middle block is addressable so it can yield")
      .not.toBeNull();
    expect(container.querySelector(".ec-right"), "the controls are addressable").not.toBeNull();
    expect(container.querySelector(".ec-marks"), "the summary is a mark row").not.toBeNull();

    const css = readFileSync("src/styles/mock-parity.css", "utf8");
    expect(css).toContain(".mock-config .ecard .ec-id { min-width: 0;");
    expect(css).toMatch(/\.mock-config \.ecard \.ec-right \{[^}]*flex: none;/);
    expect(css).toMatch(/\.mock-config \.ecard \.ec-right > \* \{[^}]*white-space: nowrap;/);
  });

  it("ProjectCard_Collapsed_CarriesNoTypeBadge", () => {
    // It read "project" on a page of nothing but projects, and when it read anything else it
    // was the declared pipeline list — which 2026-09-16-74a2 stopped asking anyone to author.
    card(project());

    expect(screen.queryByTestId("config-card-badge-sample")).toBeNull();
  });
});
