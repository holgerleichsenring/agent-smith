import { describe, it, expect, vi, beforeEach } from "vitest";
import { useState } from "react";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { TemplateBindings } from "../TemplateBindings";
import type { ConfigCatalog } from "../useConfigCatalog";
import type { StudioProject, TemplateReference } from "@/lib/configApi";
import { fetchProjectContexts } from "@/lib/configApi";

// 2026-09-15-a2d0: the template field is a row list. These tests pin what a row carries
// without being opened, and which row the fields belong to. 2026-09-14-620e's own tests
// (ProjectForm.test.tsx) still pin the fields themselves, unedited.

vi.mock("@/lib/configApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/configApi")>()),
  fetchProjectContexts: vi.fn(),
}));

const mockedContexts = vi.mocked(fetchProjectContexts);

const catalog = {
  agents: [],
  trackers: [],
  connections: [],
  // The template's own repo ref, carrying a branch nothing else could render — an
  // implementation that showed the CATALOG's branch in place of the target's default one
  // would have to print this string.
  repos: [
    { id: "web", name: "web", branch: "main" },
    { id: "api", name: "api", branch: "catalog-default-branch" },
  ],
  projects: [
    { id: "refapp", agent: "a", tracker: "t", repos: ["api"], pipeline: "", pipelines: [], resolution: null },
  ],
  "mcp-servers": [],
  secrets: [],
} as unknown as ConfigCatalog;

const template = (over: Partial<TemplateReference> = {}): TemplateReference => ({
  context: "server",
  project: "refapp",
  repo: "api",
  templateContext: "server",
  revision: "v2.1.0",
  ...over,
});

function Harness({ templates }: { templates: TemplateReference[] }) {
  const [project, setProject] = useState<StudioProject>({
    id: "proj",
    agent: "",
    tracker: "",
    repos: ["web"],
    pipeline: "",
    pipelines: [],
    resolution: null,
    templates,
  });
  return (
    <TemplateBindings
      project={project}
      catalog={catalog}
      onChange={(next) => setProject({ ...project, templates: next })}
    />
  );
}

beforeEach(() => {
  mockedContexts.mockReset();
  mockedContexts.mockResolvedValue({ contexts: ["server", "client"], unreadableReason: null });
});

describe("TemplateList", () => {
  it("TemplateList_ThreeTemplates_ShowEveryValueWithoutOpeningAnything", () => {
    render(
      <Harness
        templates={[
          template(),
          template({ context: "client", repo: "api", templateContext: "client", revision: "main" }),
          template({ context: "worker", templateContext: "worker", revision: "3f2a1c9" }),
        ]}
      />,
    );
    // Three rows, and nothing is open: the fields of no row are on screen.
    expect(screen.getByTestId("form-templates-2-row")).toBeInTheDocument();
    expect(document.querySelectorAll('[data-testid$="-editor"]')).toHaveLength(0);
    expect(screen.queryByTestId("form-templates-0-context")).toBeNull();
    // Every value of every binding is readable from the rows alone.
    for (const [i, ctx, rev] of [
      [0, "server", "v2.1.0"],
      [1, "client", "main"],
      [2, "worker", "3f2a1c9"],
    ] as const) {
      expect(screen.getByTestId(`form-templates-${i}-open`)).toHaveTextContent(ctx);
      const target = screen.getByTestId(`form-templates-${i}-target`);
      expect(target).toHaveTextContent("refapp");
      expect(target).toHaveTextContent("api");
      expect(target).toHaveTextContent(`@ ${rev}`);
    }
  });

  it("TemplateList_RendersInDeclarationOrder", () => {
    // Declaration order is load-bearing — the resolver preserves it and the consumers
    // that open and read a template iterate it — so the list never sorts.
    render(
      <Harness templates={[template({ context: "zebra" }), template({ context: "alpha" })]} />,
    );
    expect(screen.getByTestId("form-templates-0-open")).toHaveTextContent("zebra");
    expect(screen.getByTestId("form-templates-1-open")).toHaveTextContent("alpha");
  });

  it("TemplateList_SingleStoredTemplate_IsOpenOnLoad", () => {
    // One declaration is the only shape on disk today; making it cost a click to reach
    // fields that were on screen before would be a regression dressed as consistency.
    render(<Harness templates={[template()]} />);
    expect(screen.getByTestId("form-templates-0-editor")).toBeInTheDocument();
    expect(screen.getByTestId("form-templates-0-revision")).toHaveValue("v2.1.0");
  });

  it("TemplateList_RowOpens_RendersTheFieldsAndClosesTheOther", () => {
    render(<Harness templates={[template(), template({ context: "client" })]} />);
    // Two rows: nothing opens itself, because there is something to choose between.
    expect(screen.queryByTestId("form-templates-0-editor")).toBeNull();

    fireEvent.click(screen.getByTestId("form-templates-0-open"));
    expect(screen.getByTestId("form-templates-0-editor")).toBeInTheDocument();
    expect(screen.getByTestId("form-templates-0-open")).toHaveAttribute("aria-expanded", "true");

    fireEvent.click(screen.getByTestId("form-templates-1-open"));
    expect(screen.getByTestId("form-templates-1-editor")).toBeInTheDocument();
    expect(screen.queryByTestId("form-templates-0-editor")).toBeNull();

    // And a row closes itself, so the list can be read whole again.
    fireEvent.click(screen.getByTestId("form-templates-1-open"));
    expect(screen.queryByTestId("form-templates-1-editor")).toBeNull();
  });

  it("TemplateList_AddTemplate_AppendsAnOpenRow", async () => {
    render(<Harness templates={[template()]} />);
    fireEvent.click(screen.getByTestId("form-templates-add"));
    // The new row is last, open (it has nothing to read yet) and the old one is closed.
    await waitFor(() => expect(screen.getByTestId("form-templates-1-editor")).toBeInTheDocument());
    expect(screen.queryByTestId("form-templates-0-editor")).toBeNull();
    expect(screen.getByTestId("form-templates-1-unfinished")).toBeInTheDocument();
  });

  it("TemplateList_Remove_NeedsNoOpenRowAndClosesTheEditor", () => {
    render(<Harness templates={[template(), template({ context: "client" })]} />);
    fireEvent.click(screen.getByTestId("form-templates-1-open"));
    expect(screen.getByTestId("form-templates-1-editor")).toBeInTheDocument();

    // Removing the row ABOVE the open one: the open index would now name a different
    // binding, so nothing stays open.
    fireEvent.click(screen.getByTestId("form-templates-0-remove"));
    expect(screen.getByTestId("form-templates-0-open")).toHaveTextContent("client");
    expect(screen.queryByTestId("form-templates-0-editor")).toBeNull();
    expect(screen.queryByTestId("form-templates-1-row")).toBeNull();
  });

  it("TemplateList_LongRefAndSha_AreShownWhole", () => {
    // jsdom measures no layout, so what a test can pin is that the whole value reaches the
    // DOM — never shortened on its way there. That the row WRAPS rather than clips is the
    // stylesheet's (.tpl-target has no text-overflow), which no component test can see.
    const sha = "3f2a1c9d4e5b6a7c8d9e0f1a2b3c4d5e6f708192";
    render(<Harness templates={[template({ repo: "conn/Sample.Api.Boilerplate", revision: sha })]} />);
    const target = screen.getByTestId("form-templates-0-target");
    expect(target).toHaveTextContent("conn/Sample.Api.Boilerplate");
    expect(target).toHaveTextContent(sha);
  });

  it("TemplateList_MissingRevision_ReadsAsTheTargetDefaultBranch", () => {
    // An absent revision lands on the TARGET repository's own default branch — the clone
    // carries no -b — so the catalog repo entry's branch must not be shown in its place.
    render(<Harness templates={[template({ revision: null })]} />);
    const target = screen.getByTestId("form-templates-0-target");
    expect(target).toHaveTextContent("its default branch");
    expect(target).not.toHaveTextContent("catalog-default-branch");
  });

  it("TemplateList_UnfinishedBinding_IsMarkedOnTheRow", () => {
    // What a closed row can judge without an outbound call, it says. Reachability of the
    // target it does not claim — that answer only exists while the row is open.
    render(<Harness templates={[template(), template({ templateContext: "" })]} />);
    expect(screen.queryByTestId("form-templates-0-unfinished")).toBeNull();
    expect(screen.getByTestId("form-templates-1-unfinished")).toBeInTheDocument();
  });
});
