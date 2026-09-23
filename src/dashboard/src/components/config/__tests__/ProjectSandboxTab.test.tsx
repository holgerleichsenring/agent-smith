import { describe, it, expect, vi, beforeEach } from "vitest";
import { useState } from "react";
import { render, screen, fireEvent } from "@testing-library/react";
import { EntityForm } from "../EntityForm";
import type { ConfigCatalog } from "../useConfigCatalog";
import type {
  ConfigCapabilities,
  InheritedSandboxProjection,
  StudioProject,
} from "@/lib/configApi";
import { fetchConnectionRepos, fetchProjectContexts } from "@/lib/configApi";

// 2026-09-22-6968: the sixth tab. Every control is null-means-inherit, so what these pin
// is the half nothing in the product computed before: what CLEARING a control restores.
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

const processWide: InheritedSandboxProjection["processWide"] = {
  toolchainImage: { value: null, source: "run-resolved" },
  stepTimeoutSeconds: { value: 900, source: "global-default" },
  runCommandTimeoutSeconds: { value: 300, source: "global-default" },
  agentRegistry: { value: "ghcr.io/example", source: "global-default" },
  agentVersion: { value: "0.50.0", source: "global-default" },
  holdSeconds: { value: 180, source: "code-default" },
  resources: {
    values: { cpuRequest: "250m", cpuLimit: "1000m", memoryRequest: "1Gi", memoryLimit: "4Gi" },
    layer: "global-default",
  },
  images: {
    dotnet: { value: "mcr.microsoft.com/dotnet/sdk:9.0", source: "code-default" },
    node: { value: "node:20-bookworm", source: "code-default" },
  },
};

const inherited: InheritedSandboxProjection = {
  processWide,
  projects: { proj: processWide },
};

function Harness({
  initial,
  inheritedSandbox = inherited,
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

describe("ProjectForm sandbox tab", () => {
  it("ProjectForm_TheSandboxTab_RendersTheSixScalarControlsWithTheirInheritedPlaceholders", () => {
    render(<Harness />);
    openSandbox();

    expect(screen.getByTestId("form-field-sandbox-toolchainImage")).toBeInTheDocument();
    expect(screen.getByTestId("form-field-sandbox-stepTimeoutSeconds")).toHaveAttribute(
      "placeholder",
      "900",
    );
    expect(screen.getByTestId("form-field-sandbox-runCommandTimeoutSeconds")).toHaveAttribute(
      "placeholder",
      "300",
    );
    expect(screen.getByTestId("form-field-sandbox-agentRegistry")).toHaveAttribute(
      "placeholder",
      "ghcr.io/example",
    );
    expect(screen.getByTestId("form-field-sandbox-agentVersion")).toHaveAttribute(
      "placeholder",
      "0.50.0",
    );
    // 2026-09-23-2446: the sixth.
    expect(screen.getByTestId("form-field-sandbox-holdSeconds")).toHaveAttribute(
      "placeholder",
      "180",
    );
    // The provenance is said beside the control, not only implied by the placeholder.
    expect(screen.getByTestId("form-section-sandbox")).toHaveTextContent("inherits 900");
    // 2026-09-22-6c46 draws the structured three beneath them; each has its own tests.
    expect(screen.getByTestId("form-field-sandbox-resources")).toBeInTheDocument();
    expect(screen.getByTestId("form-field-sandbox-images")).toBeInTheDocument();
    expect(screen.getByTestId("form-field-sandbox-secrets")).toBeInTheDocument();
  });

  it("ProjectForm_AnOverriddenValue_ShowsTheOverrideAndStillNamesWhatClearingRestores", () => {
    render(<Harness initial={{ sandbox: { stepTimeoutSeconds: 1800 } }} />);
    openSandbox();

    expect(screen.getByTestId("form-field-sandbox-stepTimeoutSeconds")).toHaveValue(1800);
    // The placeholder is the COUNTERFACTUAL, never the project's own value back.
    expect(screen.getByTestId("form-field-sandbox-stepTimeoutSeconds")).toHaveAttribute(
      "placeholder",
      "900",
    );
  });

  it("ProjectForm_ADraftProjectWithNoResolvedRow_ShowsTheProcessWideValues", () => {
    render(<Harness initial={{ id: "brand-new" }} />);
    openSandbox();

    expect(screen.getByTestId("form-sandbox-draft-note")).toHaveTextContent(
      "not in the running configuration yet",
    );
    expect(screen.getByTestId("form-field-sandbox-stepTimeoutSeconds")).toHaveAttribute(
      "placeholder",
      "900",
    );
    expect(screen.getByTestId("form-field-sandbox-agentRegistry")).toHaveAttribute(
      "placeholder",
      "ghcr.io/example",
    );
  });

  it("ProjectForm_ARunResolvedToolchainImage_SaysItIsDetectedAtRunTimeRatherThanShowingABlank", () => {
    render(<Harness />);
    openSandbox();

    const image = screen.getByTestId("form-field-sandbox-toolchainImage");
    expect(image).not.toHaveAttribute("placeholder");
    expect(screen.getByTestId("form-section-sandbox")).toHaveTextContent(
      "detected per run from the repository",
    );
  });

  it("ProjectForm_ClearingAControl_SendsNullRatherThanZeroOrTheEmptyString", () => {
    const saved: StudioProject[] = [];
    render(
      <Harness
        initial={{ sandbox: { stepTimeoutSeconds: 1800, agentRegistry: "mirror.example" } }}
        onSave={(p) => saved.push(p)}
      />,
    );
    openSandbox();

    fireEvent.change(screen.getByTestId("form-field-sandbox-stepTimeoutSeconds"), {
      target: { value: "" },
    });
    fireEvent.change(screen.getByTestId("form-field-sandbox-agentRegistry"), {
      target: { value: "" },
    });
    fireEvent.click(screen.getByTestId("save"));

    const sent = saved[0].sandbox!;
    // Not a fake 0 and not an empty string — either would be stored as a REAL override.
    // The field leaves the browser omitted, which the API binds as the null that clears it.
    expect(sent.stepTimeoutSeconds).toBeUndefined();
    expect(sent.agentRegistry).toBeUndefined();
    const onTheWire = JSON.parse(JSON.stringify(sent)) as Record<string, unknown>;
    expect(Object.keys(onTheWire)).not.toContain("stepTimeoutSeconds");
    expect(Object.keys(onTheWire)).not.toContain("agentRegistry");
  });

  it("ProjectForm_ASaveThatNeverOpensTheSandboxTab_ChangesNoSandboxValue", () => {
    const saved: StudioProject[] = [];
    render(<Harness onSave={(p) => saved.push(p)} />);

    // Edit something on another tab entirely, then save.
    fireEvent.change(screen.getByTestId("form-ref-agent"), { target: { value: "gpt5" } });
    fireEvent.click(screen.getByTestId("save"));

    // Absent, not an empty block: the server leaves the stored block alone.
    expect(saved[0].sandbox).toBeUndefined();
  });

  it("ProjectForm_TheInheritedProjectionUnavailable_SaysSoInsteadOfDrawingBlanks", () => {
    render(<Harness inheritedSandbox={null} />);
    openSandbox();

    expect(screen.getByTestId("form-sandbox-inherited-unavailable")).toBeInTheDocument();
    expect(screen.getByTestId("form-field-sandbox-stepTimeoutSeconds")).not.toHaveAttribute(
      "placeholder",
    );
  });

  // -------------------------------------------------------------------------
  // 2026-09-23-2446: the hold window. The one control whose placeholder is live,
  // whose provenance can be an environment variable, and whose zero is a value.
  // -------------------------------------------------------------------------

  it("ProjectForm_TheHoldControl_ShowsItsInheritedValueAndProvenance", () => {
    const fromEnvironment: InheritedSandboxProjection = {
      processWide: { ...processWide, holdSeconds: { value: 240, source: "environment-variable" } },
      projects: {
        proj: { ...processWide, holdSeconds: { value: 240, source: "environment-variable" } },
      },
    };
    render(<Harness inheritedSandbox={fromEnvironment} />);
    openSandbox();

    expect(screen.getByTestId("form-field-sandbox-holdSeconds")).toHaveAttribute(
      "placeholder",
      "240",
    );
    // The leg is NAMED: sending an operator to a settings form that holds no value is
    // exactly what the environment provenance exists to prevent.
    expect(screen.getByTestId("form-section-sandbox")).toHaveTextContent(
      "inherits 240 from the SANDBOX_HOLD_SECONDS environment variable",
    );
  });

  it("ProjectForm_TheHoldControl_SaysItAppliesToTheNextTurnAndNeedsNoRestart", () => {
    render(<Harness />);
    openSandbox();

    const section = screen.getByTestId("form-section-sandbox");
    expect(section).toHaveTextContent("applies to the next turn of a design conversation");
    expect(section).toHaveTextContent("reaches a reaper scan without a server restart");
    expect(section).toHaveTextContent("0 holds nothing");
  });

  it("ProjectForm_AHoldWindowOfZero_IsKeptAsZeroAndNotReadAsInherit", () => {
    const saved: StudioProject[] = [];
    render(<Harness initial={{ sandbox: { holdSeconds: 0 } }} onSave={(p) => saved.push(p)} />);
    openSandbox();

    // A stored zero draws as a zero, not as an empty box showing the inherited 180.
    expect(screen.getByTestId("form-field-sandbox-holdSeconds")).toHaveValue(0);

    // And a zero TYPED into an empty control survives the round trip as a real override.
    fireEvent.change(screen.getByTestId("form-field-sandbox-holdSeconds"), {
      target: { value: "0" },
    });
    fireEvent.click(screen.getByTestId("save"));

    expect(saved[0].sandbox!.holdSeconds).toBe(0);
    const onTheWire = JSON.parse(JSON.stringify(saved[0].sandbox)) as Record<string, unknown>;
    expect(onTheWire.holdSeconds).toBe(0);
  });

  it("ProjectForm_TheSandboxTab_SaysWhichHalfNeedsARestart", () => {
    render(<Harness />);
    openSandbox();

    const note = screen.getByTestId("form-sandbox-inheritance-note");
    expect(note).toHaveTextContent("applies to the next run of this project");
    expect(note).toHaveTextContent("needs a server restart");
  });
});
