import { describe, it, expect, vi, beforeEach } from "vitest";
import { useState } from "react";
import { render, screen, fireEvent } from "@testing-library/react";
import { EntityForm } from "../EntityForm";
import type { ConfigCatalog } from "../useConfigCatalog";
import type {
  ConfigCapabilities,
  InheritedSandbox,
  InheritedSandboxProjection,
  StudioProject,
} from "@/lib/configApi";
import { fetchConnectionRepos, fetchProjectContexts } from "@/lib/configApi";

// 2026-09-22-6c46: the three STRUCTURED overrides, each answering the inheritance question
// its own way — one per pipeline layer, one per key, and one that inherits nothing at all.
vi.mock("@/lib/configApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/configApi")>()),
  fetchConnectionRepos: vi.fn(),
  fetchProjectContexts: vi.fn(),
}));

const catalog: ConfigCatalog = {
  agents: [{ id: "gpt5", provider: "openai", models: { coding: { model: "c" } }, keySecret: "K" }],
  trackers: [{ id: "azdo", type: "azure", organization: "o", project: "p", authSecret: "T" }],
  connections: [],
  repos: [{ id: "api", name: "api", branch: "main" }],
  projects: [],
  "mcp-servers": [],
  secrets: [{ id: "K" }, { id: "T" }],
};

const capabilities: ConfigCapabilities = {
  trackerTypes: [],
  connectionTypes: [],
  agentProviders: [],
  resolutionStrategies: [],
  permissions: [],
  builtInRoles: [],
  pipelines: [],
  roles: [],
};

const row = (over: Partial<InheritedSandbox> = {}): InheritedSandbox => ({
  toolchainImage: { value: null, source: "run-resolved" },
  stepTimeoutSeconds: { value: 900, source: "global-default" },
  runCommandTimeoutSeconds: { value: 300, source: "global-default" },
  agentRegistry: { value: "ghcr.io/example", source: "global-default" },
  agentVersion: { value: "0.50.0", source: "global-default" },
  resources: {
    values: { cpuRequest: "250m", cpuLimit: "1000m", memoryRequest: "1Gi", memoryLimit: "4Gi" },
    layer: "global-default",
  },
  images: {
    dotnet: { value: "mcr.microsoft.com/dotnet/sdk:9.0", source: "code-default" },
    node: { value: "node:20-bookworm", source: "code-default" },
  },
  ...over,
});

const projection = (r: InheritedSandbox = row()): InheritedSandboxProjection => ({
  processWide: r,
  projects: { proj: r },
});

function Harness({
  initial,
  inheritedSandbox = projection(),
  onSave,
}: {
  initial?: Partial<StudioProject>;
  inheritedSandbox?: InheritedSandboxProjection | null;
  onSave?: (p: StudioProject) => void;
} = {}) {
  const [draft, setDraft] = useState<StudioProject>({
    id: "proj",
    agent: "gpt5",
    tracker: "azdo",
    repos: ["api"],
    pipeline: "",
    pipelines: [],
    resolution: null,
    ...initial,
  });
  return (
    <>
      <EntityForm
        kind="projects"
        draft={draft}
        onChange={(n) => setDraft(n as StudioProject)}
        catalog={catalog}
        capabilities={capabilities}
        inheritedSandbox={inheritedSandbox}
        isNew={false}
        findings={[]}
      />
      <button type="button" data-testid="save" onClick={() => onSave?.(draft)}>
        save
      </button>
    </>
  );
}

beforeEach(() => {
  vi.mocked(fetchProjectContexts).mockResolvedValue({ contexts: [], unreadableReason: null });
  vi.mocked(fetchConnectionRepos).mockResolvedValue({ discoveredAt: null, repos: [] });
});

const openSandbox = () => fireEvent.click(screen.getByTestId("form-tab-sandbox"));

describe("ProjectForm structured sandbox overrides", () => {
  it("ProjectForm_TheResourceGroup_IsWhollyInheritedOrWhollyGiven", () => {
    const saved: StudioProject[] = [];
    render(<Harness onSave={(p) => saved.push(p)} />);
    openSandbox();

    // Inherited: there is no half-filled state to type into at all.
    expect(screen.queryByTestId("form-field-sandbox-resources-cpuRequest")).toBeNull();

    fireEvent.click(screen.getByTestId("form-field-sandbox-resources-override"));
    fireEvent.click(screen.getByTestId("save"));

    // Given: all four at once, seeded from what the group would have inherited.
    expect(saved[0].sandbox!.structured!.resources).toEqual({
      cpuRequest: "250m",
      cpuLimit: "1000m",
      memoryRequest: "1Gi",
      memoryLimit: "4Gi",
    });

    // And emptying ONE quantity does not produce a partial override — the group is handed
    // back whole, through its own control.
    fireEvent.change(screen.getByTestId("form-field-sandbox-resources-cpuLimit"), {
      target: { value: "" },
    });
    fireEvent.click(screen.getByTestId("save"));
    expect(saved[1].sandbox!.structured!.resources!.cpuLimit).toBe("1000m");

    fireEvent.click(screen.getByTestId("form-field-sandbox-resources-inherit"));
    fireEvent.click(screen.getByTestId("save"));
    expect(saved[2].sandbox!.structured!.resources).toBeNull();
  });

  it("ProjectForm_TheResourceGroup_NamesTheLayerThatWouldAnswerRatherThanOneNumber", () => {
    render(<Harness inheritedSandbox={projection(row({
      resources: {
        values: { cpuRequest: "100m", cpuLimit: "500m", memoryRequest: "256Mi", memoryLimit: "1Gi" },
        layer: "light-profile",
      },
    }))} />);
    openSandbox();

    const layer = screen.getByTestId("form-sandbox-resources-layer");
    expect(layer).toHaveTextContent("light profile");
    expect(layer).toHaveTextContent("cpu 100m/500m");
  });

  it("ProjectForm_TheResourceGroupWithNoInheritedValues_RefusesToSeedFourBlanks", () => {
    render(<Harness inheritedSandbox={null} />);
    openSandbox();

    // All four or none, and the only non-fabricated seed is what the group would inherit.
    expect(screen.getByTestId("form-field-sandbox-resources-override")).toBeDisabled();
    expect(screen.getByTestId("form-sandbox-resources-layer")).toHaveTextContent(
      "could not be read",
    );
  });

  it("ProjectForm_AnImageKeyTheProjectDoesNotName_ShowsTheInheritedImageAsItsPlaceholder", () => {
    render(<Harness />);
    openSandbox();

    fireEvent.click(screen.getByTestId("form-field-sandbox-images-add"));
    fireEvent.change(screen.getByTestId("form-field-sandbox-images-key-0"), {
      target: { value: "node" },
    });

    // The project names no image for node, and the row still knows what node inherits —
    // which is only possible because the projection carries the table PER KEY.
    expect(screen.getByTestId("form-field-sandbox-images-value-0")).toHaveAttribute(
      "placeholder",
      "node:20-bookworm",
    );
    expect(screen.getByTestId("form-field-sandbox-images-note-0")).toHaveTextContent(
      "inherits node:20-bookworm",
    );
  });

  it("ProjectForm_AnImageKeyTheProjectPins_ShowsItAsAnOverrideWithoutClaimingTheTableIsReplaced", () => {
    render(
      <Harness
        initial={{ sandbox: { structured: { images: { dotnet: "mirror.example/dotnet:9.0" } } } }}
      />,
    );
    openSandbox();

    expect(screen.getByTestId("form-field-sandbox-images-note-0")).toHaveTextContent(
      "overrides the code default mcr.microsoft.com/dotnet/sdk:9.0 for dotnet only",
    );
    // The rest of the table is NOT replaced, and the control says so.
    expect(screen.getByTestId("form-field-sandbox-images")).toHaveTextContent(
      "every language you do not name keeps inheriting",
    );
  });

  it("ProjectForm_TheSecretsBlock_SaysNothingIsInheritedAndOffersNoValueField", () => {
    render(<Harness />);
    openSandbox();

    expect(screen.getByTestId("form-sandbox-secrets-none-inherited")).toHaveTextContent(
      "Nothing is inherited here",
    );
    // No placeholder anywhere in the block: there is no process-wide counterpart, and a
    // blank placeholder would read as an inherited empty set.
    expect(screen.getByTestId("form-field-sandbox-secrets-env")).toBeInTheDocument();
    // The env row's VALUE is a secretName:key reference, and the file rows carry no value
    // field at all — the values stay in the cluster.
    expect(screen.getByTestId("form-field-sandbox-secrets")).toHaveTextContent(
      "the values stay in the cluster",
    );
    expect(screen.queryByTestId("form-field-sandbox-secrets-files-value-0")).toBeNull();
  });

  it("ProjectForm_TheFileMountControl_EditsMountSecretAndKeyRows", () => {
    const saved: StudioProject[] = [];
    render(<Harness onSave={(p) => saved.push(p)} />);
    openSandbox();

    fireEvent.click(screen.getByTestId("form-field-sandbox-secrets-files-add"));
    fireEvent.change(screen.getByTestId("form-field-sandbox-secrets-files-mount-0"), {
      target: { value: "/secrets/server.key" },
    });
    fireEvent.change(screen.getByTestId("form-field-sandbox-secrets-files-secret-0"), {
      target: { value: "sf-creds" },
    });
    fireEvent.change(screen.getByTestId("form-field-sandbox-secrets-files-key-0"), {
      target: { value: "jwt-key" },
    });
    fireEvent.click(screen.getByTestId("save"));

    expect(saved.at(-1)!.sandbox!.structured!.secrets!.files).toEqual([
      { mount: "/secrets/server.key", secret: "sf-creds", key: "jwt-key" },
    ]);
  });

  it("ProjectForm_TheFileMountControl_DroppingItsLastRow_DeclaresNoneRatherThanAnEmptyList", () => {
    const saved: StudioProject[] = [];
    render(
      <Harness
        initial={{
          sandbox: {
            structured: {
              secrets: { files: [{ mount: "/s/k", secret: "sf-creds", key: "jwt" }] },
            },
          },
        }}
        onSave={(p) => saved.push(p)}
      />,
    );
    openSandbox();

    fireEvent.click(screen.getByTestId("form-field-sandbox-secrets-files-remove-0"));
    fireEvent.click(screen.getByTestId("save"));

    expect(saved[0].sandbox!.structured!.secrets).toBeNull();
  });

  it("ProjectForm_DeletingTheLastMapRow_WritesInheritNotAnEmptyMap", () => {
    const saved: StudioProject[] = [];
    render(
      <Harness
        initial={{ sandbox: { structured: { images: { dotnet: "mirror.example/dotnet:9.0" } } } }}
        onSave={(p) => saved.push(p)}
      />,
    );
    openSandbox();

    fireEvent.click(screen.getByTestId("form-field-sandbox-images-remove-0"));
    fireEvent.click(screen.getByTestId("save"));

    // Null, never {} — a project cannot declare "an empty map", and an empty one would
    // read as "inherit nothing" rather than "inherit the code defaults".
    const images = saved[0].sandbox!.structured!.images;
    expect(images).toBeNull();
    expect(images).not.toEqual({});
  });

  it("ProjectForm_ASaveThatNeverOpensTheSandboxTab_SendsNoStructuredBlockAtAll", () => {
    const saved: StudioProject[] = [];
    render(<Harness onSave={(p) => saved.push(p)} />);

    fireEvent.change(screen.getByTestId("form-ref-agent"), { target: { value: "gpt5" } });
    fireEvent.click(screen.getByTestId("save"));

    // Absent, so the server leaves the stored resources, image pins and secret names alone.
    expect(saved[0].sandbox).toBeUndefined();
  });
});
