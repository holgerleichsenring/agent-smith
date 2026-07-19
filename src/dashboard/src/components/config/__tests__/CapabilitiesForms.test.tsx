import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { ConfigStudio } from "../ConfigStudio";

// p0345c: the studio forms KNOW the domain — but only through the backend's
// capabilities descriptor. Type/provider dropdowns and the per-type field sets
// render from it; switching type swaps the field set; nothing is hardcoded.

vi.mock("@/lib/configApi", () => {
  const client = <T,>(rows: T[]) => ({
    list: vi.fn().mockResolvedValue(rows),
    create: vi.fn().mockResolvedValue(rows[0] ?? { id: "x" }),
    update: vi.fn().mockResolvedValue(rows[0] ?? { id: "x" }),
    remove: vi.fn().mockResolvedValue(undefined),
  });
  return {
    agentsApi: client([]),
    trackersApi: client([]),
    connectionsApi: client([]),
    reposApi: client([]),
    projectsApi: client([]),
    mcpServersApi: client([]),
    secretsApi: client([{ id: "PAT" }, { id: "KEY" }]),
    fetchChanges: vi.fn().mockResolvedValue([]),
    revertChange: vi.fn(),
    fetchConfigExportYml: vi.fn(),
    fetchCapabilities: vi.fn().mockResolvedValue({
      trackerTypes: [
        {
          type: "azure-devops",
          fields: [
            { key: "organization", label: "organization", required: true },
            { key: "project", label: "project", required: true },
            { key: "triggerStatuses", label: "trigger statuses", required: false },
          ],
        },
        {
          type: "github",
          fields: [
            { key: "url", label: "repository url", required: true },
            { key: "openStates", label: "open states", required: false },
          ],
        },
      ],
      connectionTypes: [
        {
          type: "github",
          orgLabel: "owner",
          fields: [{ key: "organization", label: "organization", required: true }],
        },
      ],
      agentProviders: ["azure-openai", "anthropic"],
      resolutionStrategies: ["tag", "repo"],
      pipelines: ["feature-implementation"],
      roles: [
        { key: "coding", optional: false },
        { key: "primary", optional: false },
        { key: "reasoning", optional: true },
      ],
    }),
    fetchConnectionRepos: vi.fn().mockResolvedValue({ discoveredAt: null, repos: [] }),
  };
});

beforeEach(() => vi.clearAllMocks());

describe("Capabilities-driven forms (p0345c)", () => {
  it("Forms_RenderPerTypeFields_FromCapabilities", async () => {
    render(<ConfigStudio section="trackers" />);
    await screen.findByTestId("config-new-trackers");
    fireEvent.click(screen.getByTestId("config-new-trackers"));

    // TYPE is a dropdown fed from capabilities — never a text input.
    const type = screen.getByTestId("form-field-type");
    expect(type.tagName).toBe("SELECT");
    await waitFor(() => expect(type.querySelector('option[value="azure-devops"]')).not.toBeNull());
    expect(type.querySelector('option[value="github"]')).not.toBeNull();
    // No type picked → no per-type fields yet.
    expect(screen.queryByTestId("form-field-organization")).toBeNull();

    // azure-devops declares organization/project (+ trigger statuses list).
    fireEvent.change(type, { target: { value: "azure-devops" } });
    expect(screen.getByTestId("form-field-organization")).toBeInTheDocument();
    expect(screen.getByTestId("form-field-project")).toBeInTheDocument();
    expect(screen.getByTestId("form-field-triggerStatuses")).toBeInTheDocument();
    expect(screen.queryByTestId("form-field-url")).toBeNull();

    // Switching type swaps the field set to what THAT type declares.
    fireEvent.change(screen.getByTestId("form-field-type"), { target: { value: "github" } });
    expect(screen.getByTestId("form-field-url")).toBeInTheDocument();
    expect(screen.getByTestId("form-field-openStates")).toBeInTheDocument();
    expect(screen.queryByTestId("form-field-organization")).toBeNull();
    expect(screen.queryByTestId("form-field-triggerStatuses")).toBeNull();
  });

  it("TrackerForm_RequiredFields_GateSave", async () => {
    render(<ConfigStudio section="trackers" />);
    await screen.findByTestId("config-new-trackers");
    fireEvent.click(screen.getByTestId("config-new-trackers"));

    const type = screen.getByTestId("form-field-type");
    await waitFor(() => expect(type.querySelector('option[value="github"]')).not.toBeNull());

    fireEvent.change(screen.getByTestId("form-field-id"), { target: { value: "gh" } });
    // Type not picked → blocked.
    expect(screen.getByTestId("config-drawer-save")).toBeDisabled();
    fireEvent.change(type, { target: { value: "github" } });
    // Required url still empty → blocked.
    expect(screen.getByTestId("config-drawer-save")).toBeDisabled();
    fireEvent.change(screen.getByTestId("form-field-url"), { target: { value: "https://github.com/acme" } });
    expect(screen.getByTestId("config-drawer-save")).not.toBeDisabled();
  });

  it("ConnectionForm_OrgLabel_NamesTheOrgField", async () => {
    render(<ConfigStudio section="connections" />);
    await screen.findByTestId("config-new-connections");
    fireEvent.click(screen.getByTestId("config-new-connections"));

    const type = screen.getByTestId("form-field-type");
    await waitFor(() => expect(type.querySelector('option[value="github"]')).not.toBeNull());
    fireEvent.change(type, { target: { value: "github" } });

    // The org field renders under the TYPE's own name for it.
    const org = screen.getByTestId("form-field-organization");
    const label = org.closest(".field")?.querySelector("label");
    expect(label?.textContent).toContain("owner");
  });

  it("AgentForm_SectionedDrawer_ProviderFromCapabilities_EmptySectionsNotPersisted", async () => {
    const { agentsApi } = await import("@/lib/configApi");
    render(<ConfigStudio section="agents" />);
    await screen.findByTestId("config-new-agents");
    fireEvent.click(screen.getByTestId("config-new-agents"));

    // Provider is a dropdown from capabilities.agentProviders.
    const provider = screen.getByTestId("form-field-provider");
    expect(provider.tagName).toBe("SELECT");
    await waitFor(() => expect(provider.querySelector('option[value="anthropic"]')).not.toBeNull());

    // The full surface is SECTIONED; optional sections start collapsed + unset.
    expect(screen.getByTestId("agent-section-provider")).toHaveAttribute("data-open", "true");
    expect(screen.getByTestId("agent-section-models")).toHaveAttribute("data-open", "true");
    for (const s of ["pricing", "cache", "compaction", "retry"]) {
      expect(screen.getByTestId(`agent-section-${s}`)).toHaveAttribute("data-open", "false");
    }

    fireEvent.change(screen.getByTestId("form-field-id"), { target: { value: "claude" } });
    fireEvent.change(provider, { target: { value: "anthropic" } });

    // Roles are the FIXED set from capabilities — "coding" is a required row
    // (no free-text add-role box); set its model + maxTokens directly.
    fireEvent.change(screen.getByTestId("form-field-coding"), { target: { value: "claude-fable-5" } });
    fireEvent.change(screen.getByTestId("form-field-coding-maxTokens"), { target: { value: "64000" } });

    // Open Cache, add settings, then REMOVE them again — must not persist.
    fireEvent.click(screen.getByTestId("agent-section-cache-toggle"));
    fireEvent.click(screen.getByTestId("agent-add-cache"));
    expect(screen.getByTestId("form-field-cache-isEnabled")).toHaveAttribute("data-selected", "true");
    fireEvent.click(screen.getByTestId("agent-clear-cache"));

    fireEvent.click(screen.getByTestId("config-drawer-save"));
    await waitFor(() => expect(agentsApi.create).toHaveBeenCalledTimes(1));
    const saved = vi.mocked(agentsApi.create).mock.calls[0][0] as {
      provider: string;
      models: Record<string, { model: string; maxTokens?: number }>;
      pricing?: unknown;
      cache?: unknown;
      compaction?: unknown;
      retry?: unknown;
    };
    expect(saved.provider).toBe("anthropic");
    expect(saved.models.coding).toEqual({ model: "claude-fable-5", maxTokens: 64000 });
    // Only non-empty sections are persisted.
    expect(saved.pricing).toBeUndefined();
    expect(saved.cache).toBeUndefined();
    expect(saved.compaction).toBeUndefined();
    expect(saved.retry).toBeUndefined();
  });
});
