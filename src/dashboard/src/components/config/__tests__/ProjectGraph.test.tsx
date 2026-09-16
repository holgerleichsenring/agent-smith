import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { ProjectGraph, fitLabel } from "../ProjectGraph";
import type { ConfigCatalog } from "../useConfigCatalog";
import type { StudioProject, TemplateReference } from "@/lib/configApi";

// 2026-09-16-4b41: the wiring drawing, five columns.
//
// A template belongs to a CONTEXT and a context belongs to a REPOSITORY — three things and
// two arrows. 942a drew the contexts inside their repository's rectangle, which left one of
// those arrows visible; nesting hid the two the drawing exists to show. These tests moved
// here from ProjectCard.test.tsx, which was already half graph, and the ones that read the
// old blocks were rewritten against rows.

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

const draw = (p: StudioProject) => render(<ProjectGraph project={p} catalog={CATALOG} />);

/** The viewBox's height — the drawing's own arithmetic, readable from the DOM. */
const viewHeight = () => Number(screen.getByRole("img").getAttribute("viewBox")!.split(" ")[3]);

const rectOf = (testId: string) => screen.getByTestId(testId).querySelector("rect")!;

describe("ProjectGraph", () => {
  it("ProjectGraph_DrawsFiveColumnHeaders", () => {
    // The change is named after them: without a header a column of bare names says nothing
    // about what kind of thing it lists.
    draw(project({ templates: [template()] }));

    const headers = ["agent", "project", "repositories", "contexts", "built-after"].map((k) =>
      screen.getByTestId(`graph-header-sample-${k}`),
    );
    expect(headers.map((h) => h.textContent)).toEqual([
      "AGENT · TRACKER",
      "PROJECT",
      "REPOSITORIES",
      "CONTEXTS",
      "BUILT AFTER",
    ]);
    // They sit left-to-right in the order they name.
    const xs = headers.map((h) => Number(h.getAttribute("x")));
    expect([...xs].sort((a, b) => a - b)).toEqual(xs);
    // Sized for the SCALED result — the studio's main column caps at 1000px, so at the
    // reference's 10 units these would land near eight pixels.
    for (const h of headers) expect(Number(h.getAttribute("font-size"))).toBeGreaterThan(10);
  });

  it("ProjectGraph_OneRowPerRepositoryAndContextPair", () => {
    // Three columns, two arrows: the repository, the context it declares, and the template
    // that context is built after. The context is NOT inside the repository's rectangle.
    draw(project({ templates: [template(), template({ context: "client", contextRepo: "web", repo: "api" })] }));

    const apiRepo = screen.getByTestId("graph-node-repo-sample-api");
    const apiCtx = screen.getByTestId("graph-node-context-sample-api-server-0");
    const webCtx = screen.getByTestId("graph-node-context-sample-web-client-0");
    expect(apiRepo.contains(apiCtx), "the context is its own column, not nested").toBe(false);

    // Each column is at its own x, and the three grow left to right.
    const x = (n: Element) => Number(n.querySelector("rect")!.getAttribute("x"));
    expect(x(apiRepo)).toBeLessThan(x(apiCtx));
    expect(x(apiCtx)).toBeLessThan(x(screen.getByTestId("graph-node-template-sample-api-server-0")));
    expect(x(apiCtx)).toBe(x(webCtx));

    // A pair shares one row: the context and its template are centred on the same line,
    // even though the two boxes are not the same height.
    const mid = (id: string) => {
      const r = rectOf(id);
      return Number(r.getAttribute("y")) + Number(r.getAttribute("height")) / 2;
    };
    expect(mid("graph-node-context-sample-api-server-0"))
      .toBe(mid("graph-node-template-sample-api-server-0"));
    // …and the two pairs sit on different rows.
    expect(mid("graph-node-context-sample-web-client-0"))
      .toBeGreaterThan(mid("graph-node-context-sample-api-server-0"));

    expect(screen.getByRole("img").getAttribute("aria-label")).toContain("2 repositories");
  });

  it("ProjectGraph_TwoReposDeclaringOneContextName_AreTwoRows", () => {
    // The case 4df5 exists for: one context NAME declared by two repositories. As chips they
    // were one name; as rows each carries the repository it belongs to beside it.
    draw(
      project({
        templates: [
          template({ context: "default", contextRepo: "api" }),
          template({ context: "default", contextRepo: "web" }),
        ],
      }),
    );

    const first = rectOf("graph-node-context-sample-api-default-0");
    const second = rectOf("graph-node-context-sample-web-default-0");
    expect(Number(second.getAttribute("y"))).toBeGreaterThan(Number(first.getAttribute("y")));
    expect(screen.getByTestId("graph-node-context-sample-api-default-0").querySelector("title")!.textContent)
      .toBe("context: default");
  });

  it("ProjectGraph_RepositoryNoTemplateNames_StillGetsARow", () => {
    draw(project({ templates: [template()] }));

    const web = screen.getByTestId("graph-node-repo-sample-web");
    expect(web).toBeInTheDocument();
    // `web` is drawn, and nothing is invented beside it.
    expect(screen.queryByTestId("graph-node-context-sample-web-server-0")).toBeNull();
    expect(document.querySelectorAll('[data-testid^="graph-node-context-"]')).toHaveLength(1);
    // Its row is its own: the repository with no context sits below the one that has one.
    expect(Number(rectOf("graph-node-repo-sample-web").getAttribute("y")))
      .toBeGreaterThan(Number(rectOf("graph-node-repo-sample-api").getAttribute("y")));
  });

  it("ProjectGraph_ManyReposAndContexts_HeightGrows", () => {
    // 942a's height was a sum of block chrome — a rectangle per repository plus a row per
    // context inside it. It follows the ROW COUNT now.
    const three = draw(project({ repos: ["a", "b", "c"] }));
    const small = viewHeight();
    three.unmount();

    const six = draw(project({ repos: ["a", "b", "c", "d", "e", "f"] }));
    const big = viewHeight();
    six.unmount();
    expect(big).toBeGreaterThan(small);
    expect(big - small).toBe(3 * (viewHeightPerRow(small, 3)));

    // A repository declaring two contexts is two rows, not one.
    draw(project({ repos: ["a"], templates: [template({ contextRepo: "a" }), template({ contextRepo: "a", context: "client" })] }));
    expect(viewHeight()).toBe(small - viewHeightPerRow(small, 3)); // 2 rows vs 3
  });

  it("ProjectGraph_LabelLongerThanItsBox_IsTruncatedAndKeepsItsTitle", () => {
    // SVG text does not wrap, and a real repository ref is three times the width of any name
    // a mock uses — it ran out of its box and across the next column.
    const ref = "a-long-connection-name/Some.Very.Long.Repository.Name";
    draw(project({ repos: [ref] }));

    const node = screen.getByTestId(`graph-node-repo-sample-${ref}`);
    const drawn = node.querySelector("text:last-of-type")!.textContent!;
    expect(drawn.endsWith("…"), `"${drawn}" is truncated`).toBe(true);
    expect(drawn.length).toBeLessThan(ref.length);
    expect(ref.startsWith(drawn.slice(0, -1))).toBe(true);
    // The full name is still readable on hover.
    expect(node.querySelector("title")!.textContent).toBe(`repository: ${ref}`);
  });

  it("ProjectGraph_LabelInsideTheBudget_IsNotTruncated", () => {
    // A built-after label is a project, a repo and a context joined, so truncation fires on
    // ordinary data. This one sits just inside the budget and must survive untouched.
    draw(project({ repos: ["api"], templates: [template()] }));

    const node = screen.getByTestId("graph-node-template-sample-api-server-0");
    expect(node.querySelector("text:last-of-type")!.textContent).toBe("refapp / api · server");
    expect(node.querySelector("title")!.textContent).toBe("built after: refapp / api · server");
  });

  it("FitLabel_IsAMonospaceCharacterBudget", () => {
    // budget = floor((boxWidth - 18) / (0.6 * fontSize)); the advance holds for the stack
    // these labels use, and a narrower face only over-truncates, which is the safe direction.
    expect(fitLabel("abcdefghij", 100, 12)).toBe("abcdefghij"); // budget 11
    expect(fitLabel("abcdefghijkl", 100, 12)).toBe("abcdefghij…"); // budget 11, 10 + ellipsis
    expect(fitLabel("abc", 10, 12)).toBe("…"); // no room for anything
  });

  it("ProjectGraph_UnfinishedTemplate_IsTheOnlyColouredThing", () => {
    // The drawing has ONE problem colour; unfinished keeps using it. Telling unfinished apart
    // from unresolved is the card's mark row, not the drawing's business.
    draw(project({ templates: [template(), template({ context: "client", contextRepo: "web", templateContext: "" })] }));

    const coloured = [...document.querySelectorAll('[data-coloured="true"]')].map((n) =>
      n.getAttribute("data-testid"),
    );
    expect(coloured).toEqual([
      "graph-node-context-sample-web-client-0",
      "graph-node-template-sample-web-client-0",
    ]);
  });
});

/** The row pitch, read back out of a drawing whose row count is known. */
function viewHeightPerRow(height: number, rows: number): number {
  return (height - 44) / rows;
}
