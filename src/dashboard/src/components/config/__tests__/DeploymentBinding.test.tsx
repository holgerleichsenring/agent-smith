import { describe, it, expect, vi, beforeEach } from "vitest";
import { useState } from "react";
import { render, screen, fireEvent } from "@testing-library/react";
import { TemplateBindings } from "../TemplateBindings";
import { isTemplateUnfinished } from "../integrity";
import type { ConfigCatalog } from "../useConfigCatalog";
import type { StudioProject, TemplateReference } from "@/lib/configApi";
import { fetchProjectContexts } from "@/lib/configApi";

// 2026-11-18-3863a: a binding's kind, and a deployment binding's environments.

vi.mock("@/lib/configApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/configApi")>()),
  fetchProjectContexts: vi.fn(),
}));

const catalog = {
  agents: [], trackers: [], connections: [],
  repos: [{ id: "app", name: "app", branch: "main" }, { id: "gitops", name: "gitops", branch: "main" }],
  projects: [{ id: "platform", agent: "a", tracker: "t", repos: ["delivery"], pipeline: "", pipelines: [], resolution: null }],
  "mcp-servers": [], "design-sources": [], secrets: [],
} as unknown as ConfigCatalog;

const binding = (over: Partial<TemplateReference> = {}): TemplateReference => ({
  context: "api", project: "platform", repo: "delivery", templateContext: "k8s-gitops", revision: "v1", ...over,
});

function Harness({ templates, onSaved }: { templates: TemplateReference[]; onSaved: (t: TemplateReference[]) => void }) {
  const [project, setProject] = useState<StudioProject>({
    id: "proj", agent: "", tracker: "", repos: ["app"], pipeline: "", pipelines: [], resolution: null, templates,
  } as unknown as StudioProject);
  return (
    <TemplateBindings project={project} catalog={catalog}
      onChange={(next) => { onSaved(next); setProject({ ...project, templates: next }); }} />
  );
}

beforeEach(() => {
  vi.mocked(fetchProjectContexts).mockReset();
  vi.mocked(fetchProjectContexts).mockResolvedValue({ contexts: ["api"], unreadableReason: null });
});

describe("DeploymentBinding", () => {
  it("DeploymentBinding_CodeKind_ShowsNoEnvironments", () => {
    render(<Harness templates={[binding()]} onSaved={() => {}} />);
    fireEvent.click(screen.getByTestId("form-templates-0-open"));

    expect(screen.getByTestId("form-templates-0-kind")).toHaveValue("code");
    expect(screen.queryByTestId("form-templates-0-environments")).toBeNull();
    expect(screen.getByTestId("form-templates-0-target")).toHaveTextContent("built after");
  });

  it("DeploymentBinding_SwitchToDeployment_AddsAnEnvironmentWithApprovalOn", () => {
    const saved: TemplateReference[][] = [];
    render(<Harness templates={[binding()]} onSaved={(t) => saved.push(t)} />);
    fireEvent.click(screen.getByTestId("form-templates-0-open"));

    fireEvent.change(screen.getByTestId("form-templates-0-kind"), { target: { value: "deployment" } });
    fireEvent.click(screen.getByTestId("form-templates-0-env-add"));
    fireEvent.change(screen.getByTestId("form-templates-0-env-0-name"), { target: { value: "test" } });
    fireEvent.change(screen.getByTestId("form-templates-0-env-0-repo"), { target: { value: "gitops" } });

    const last = saved[saved.length - 1][0];
    expect(last.kind).toBe("deployment");
    expect(last.environments).toEqual([{ name: "test", repo: "gitops", approval: null, parameters: null }]);
    expect(screen.getByTestId("form-templates-0-env-0-approval")).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByTestId("form-templates-0-target")).toHaveTextContent("delivered after");
    expect(screen.getByTestId("form-templates-0-envs")).toHaveTextContent("test");
  });

  it("DeploymentBinding_BackToCode_DropsTheEnvironments", () => {
    const saved: TemplateReference[][] = [];
    render(<Harness templates={[binding({ kind: "deployment", environments: [{ name: "test" }] })]} onSaved={(t) => saved.push(t)} />);
    fireEvent.click(screen.getByTestId("form-templates-0-open"));

    fireEvent.change(screen.getByTestId("form-templates-0-kind"), { target: { value: "code" } });

    expect(saved[saved.length - 1][0]).toMatchObject({ kind: null, environments: null });
  });

  it("DeploymentBinding_UnnamedEnvironment_IsUnfinished", () => {
    expect(isTemplateUnfinished(binding({ kind: "deployment", environments: [{ name: "" }] }))).toBe(true);
    expect(isTemplateUnfinished(binding({ kind: "deployment", environments: [{ name: "test" }] }))).toBe(false);
  });
});
