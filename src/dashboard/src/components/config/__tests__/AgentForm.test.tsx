import { describe, it, expect, vi, beforeEach } from "vitest";
import { useState } from "react";
import { render, screen, fireEvent, within } from "@testing-library/react";
import { AgentForm } from "../AgentForm";
import type { ConfigCatalog } from "../useConfigCatalog";
import type { ConfigCapabilities, ModelPriceList, StudioAgent } from "@/lib/configApi";
import { fetchModelPrices } from "@/lib/configApi";
import { resetPriceListCache } from "../agent/usePriceList";

// 2026-09-30-62bab: the agent drawer declares models once in a catalog and lets each
// activity pick an entry. The price list is the only network read the form makes.

vi.mock("@/lib/configApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/configApi")>()),
  fetchModelPrices: vi.fn(),
}));

const mockedPrices = vi.mocked(fetchModelPrices);

const PRICES: ModelPriceList = {
  source: "test",
  fetchedAt: "2026-09-30T06:00:00Z",
  models: [
    { id: "gpt-4.1", provider: "openai", inputPerMillion: 2, outputPerMillion: 8, cacheReadPerMillion: 0.5, contextWindowTokens: 1047576 },
    { id: "gpt-4.1-mini", provider: "openai", inputPerMillion: 0.4, outputPerMillion: 1.6, cacheReadPerMillion: 0.1, contextWindowTokens: 1047576 },
    { id: "azure/gpt-5.6", provider: "azure", inputPerMillion: 1.25, outputPerMillion: 10, cacheReadPerMillion: null, contextWindowTokens: 400000 },
  ],
};

const capabilities: ConfigCapabilities = {
  trackerTypes: [],
  connectionTypes: [],
  agentProviders: ["azure-openai", "anthropic"],
  resolutionStrategies: [],
  pipelines: [],
  permissions: [],
  builtInRoles: [],
  roles: [
    { key: "primary", optional: false, needsStrong: true },
    { key: "scout", optional: true, needsStrong: false },
    { key: "planning", optional: true, needsStrong: true },
    { key: "contextGeneration", optional: true, needsStrong: true },
    { key: "codeMapGeneration", optional: true, needsStrong: true },
  ],
};

const catalog: ConfigCatalog = {
  agents: [],
  trackers: [],
  connections: [],
  repos: [],
  projects: [],
  "mcp-servers": [],
  "design-sources": [],
  secrets: [],
};

const BASE: StudioAgent = {
  id: "azure",
  provider: "azure-openai",
  keySecret: null,
  catalog: {
    "gpt-4.1": { model: "gpt-4.1", deployment: "gpt41", tier: "strong", contextWindowTokens: 1047576 },
    mini: { model: "gpt-4.1-mini", tier: "fast" },
  },
  models: { primary: "gpt-4.1" },
};

let latest: StudioAgent = BASE;

function Harness({ initial = BASE }: { initial?: StudioAgent }) {
  const [draft, setDraft] = useState<StudioAgent>(initial);
  latest = draft;
  return (
    <AgentForm
      draft={draft}
      onChange={(next) => {
        latest = next;
        setDraft(next);
      }}
      catalog={catalog}
      capabilities={capabilities}
    />
  );
}

const openTab = (key: string) => fireEvent.click(screen.getByTestId(`agent-tab-${key}`));

beforeEach(() => {
  vi.clearAllMocks();
  resetPriceListCache();
  mockedPrices.mockResolvedValue(PRICES);
  latest = BASE;
});

describe("AgentForm (2026-09-30-62bab)", () => {
  it("AddModel_PickedFromList_PrefillsPriceFromTheList", async () => {
    render(<Harness />);
    await screen.findByTestId("agent-models-source");
    await screen.findByText(/public price list/);
    fireEvent.click(screen.getByTestId("agent-model-add"));

    fireEvent.change(screen.getByTestId("agent-model-id"), { target: { value: "gpt-5" } });
    fireEvent.click(screen.getByTestId("agent-model-id-match-azure/gpt-5.6"));

    expect(screen.getByTestId("agent-model-id")).toHaveValue("azure/gpt-5.6");
    expect(screen.getByTestId("agent-model-preview")).toHaveTextContent("$1.25 / $10.00 / —");
    expect(screen.getByTestId("agent-model-preview-source")).toHaveTextContent("price list");
    // No price was typed and none is written: the list prices it.
    fireEvent.click(screen.getByTestId("agent-model-apply"));
    expect(latest.catalog["azure/gpt-5.6"]).toEqual({ model: "azure/gpt-5.6" });
    expect(latest.pricing).toBeUndefined();
  });

  it("AddModel_Window_FillsOnlyOnClick", async () => {
    render(<Harness />);
    await screen.findByText(/public price list/);
    fireEvent.click(screen.getByTestId("agent-model-add"));
    fireEvent.change(screen.getByTestId("agent-model-id"), { target: { value: "gpt-4.1-mini" } });

    // Offered, labelled, not filled.
    const offer = screen.getByTestId("agent-model-window-from-list");
    expect(offer).toHaveTextContent("use list value (1,047,576)");
    expect(offer).toHaveTextContent("from price list");
    expect(screen.getByTestId("agent-model-window")).toHaveValue(null);

    fireEvent.click(offer);
    expect(screen.getByTestId("agent-model-window")).toHaveValue(1047576);
    expect(screen.queryByTestId("agent-model-window-from-list")).toBeNull();
  });

  it("AddModel_Override_WritesPricingForTheModelId_UntoggleClearsIt", async () => {
    render(<Harness />);
    await screen.findByText(/public price list/);
    fireEvent.click(screen.getByTestId("agent-model-card-gpt-4.1"));
    fireEvent.click(screen.getByTestId("agent-model-override"));
    // The override starts from the list price.
    expect(screen.getByTestId("agent-model-override-input")).toHaveValue(2);
    fireEvent.change(screen.getByTestId("agent-model-override-input"), { target: { value: "1.5" } });
    fireEvent.click(screen.getByTestId("agent-model-apply"));
    expect(latest.pricing).toEqual({ models: { "gpt-4.1": { inputPerMillion: 1.5, outputPerMillion: 8, cacheReadPerMillion: 0.5 } } });
    expect(screen.getByTestId("agent-model-source-gpt-4.1")).toHaveTextContent("override");

    fireEvent.click(screen.getByTestId("agent-model-card-gpt-4.1"));
    fireEvent.click(screen.getByTestId("agent-model-override"));
    fireEvent.click(screen.getByTestId("agent-model-apply"));
    // An EMPTY table clears the stored one; an absent table would keep it.
    expect(latest.pricing).toEqual({ models: {} });
  });

  it("ModelCard_SourceLabel_OverrideVsPriceListVsNotInList", async () => {
    render(
      <Harness
        initial={{
          ...BASE,
          catalog: {
            ...BASE.catalog,
            eu: { model: "gpt41-eu-prod" },
            // A bare name answers from a provider-prefixed list id.
            next: { model: "gpt-5.6" },
          },
          pricing: { models: { "GPT-4.1-MINI": { inputPerMillion: 0.3, outputPerMillion: 1.2 } } },
        }}
      />,
    );
    await screen.findByText(/public price list/);

    expect(screen.getByTestId("agent-model-source-gpt-4.1")).toHaveTextContent("price list");
    expect(screen.getByTestId("agent-model-price-gpt-4.1")).toHaveTextContent("$2.00 / $8.00 / $0.50");
    expect(screen.getByTestId("agent-model-source-mini")).toHaveTextContent("override");
    expect(screen.getByTestId("agent-model-price-mini")).toHaveTextContent("$0.30 / $1.20 / —");
    expect(screen.getByTestId("agent-model-source-next")).toHaveTextContent("price list");
    expect(screen.getByTestId("agent-model-source-eu")).toHaveTextContent("not in list");
    expect(screen.getByTestId("agent-models-unpriced")).toHaveTextContent("eu");
    expect(screen.getByTestId("agent-model-used-gpt-4.1")).toHaveTextContent("used by Coding");
    expect(screen.getByTestId("agent-model-used-mini")).toHaveTextContent("not assigned");
  });

  it("ActivitySelect_OffersOnlyCatalogEntries_AndAFollowsOptionForOptionalRoles", async () => {
    render(<Harness />);
    openTab("activities");

    const values = (role: string) =>
      Array.from((screen.getByTestId(`agent-activity-select-${role}`) as HTMLSelectElement).options).map((o) => o.value);
    // Coding: entries only — it follows nothing.
    expect(values("primary")).toEqual(["gpt-4.1", "mini"]);
    // An optional role: follows, then the entries.
    expect(values("planning")).toEqual(["", "gpt-4.1", "mini"]);
    expect(screen.getByTestId("agent-activity-follows-planning")).toHaveTextContent("follows Coding → gpt-4.1");
    // Code map follows scout once scout is set.
    fireEvent.change(screen.getByTestId("agent-activity-select-scout"), { target: { value: "mini" } });
    expect(screen.getByTestId("agent-activity-follows-codeMapGeneration")).toHaveTextContent("follows Quick lookups → mini");
  });

  it("ActivityTab_FastEntryOnANeedsStrongRole_IsMarked", async () => {
    render(<Harness />);
    expect(screen.getByTestId("agent-tab-activities")).toHaveAttribute("data-marked", "false");
    openTab("activities");

    fireEvent.change(screen.getByTestId("agent-activity-select-contextGeneration"), { target: { value: "mini" } });
    expect(screen.getByTestId("agent-tab-activities")).toHaveAttribute("data-marked", "true");
    expect(screen.getByTestId("agent-activity-mismatch-contextGeneration")).toHaveTextContent(
      "a fast model on an activity that needs a strong one",
    );
    fireEvent.change(screen.getByTestId("agent-activity-select-contextGeneration"), { target: { value: "gpt-4.1" } });
    expect(screen.getByTestId("agent-tab-activities")).toHaveAttribute("data-marked", "false");
  });

  it("ActivityTab_FastScout_IsFineForLookups_ButMarksTheCodeMapThatFollowsIt", async () => {
    render(<Harness />);
    openTab("activities");

    fireEvent.change(screen.getByTestId("agent-activity-select-scout"), { target: { value: "mini" } });
    // Quick lookups needs no strong model; Code map does, and follows scout.
    expect(screen.queryByTestId("agent-activity-mismatch-scout")).toBeNull();
    expect(screen.getByTestId("agent-activity-mismatch-codeMapGeneration")).toBeInTheDocument();
    expect(screen.getByTestId("agent-tab-activities")).toHaveAttribute("data-marked", "true");
  });

  it("SavedPayload_IsCatalogPlusRoleNames", async () => {
    render(<Harness />);
    openTab("activities");
    fireEvent.change(screen.getByTestId("agent-activity-select-scout"), { target: { value: "mini" } });
    fireEvent.change(screen.getByTestId("agent-activity-select-planning"), { target: { value: "gpt-4.1" } });
    // Back to follows: the role is removed, not stored as "".
    fireEvent.change(screen.getByTestId("agent-activity-select-planning"), { target: { value: "" } });

    expect(latest.models).toEqual({ primary: "gpt-4.1", scout: "mini" });
    expect(latest.catalog).toEqual(BASE.catalog);
  });

  it("RemoveEntry_UsedByAnActivity_IsRefusedWithTheReason", async () => {
    render(<Harness />);
    fireEvent.click(screen.getByTestId("agent-model-card-gpt-4.1"));

    expect(screen.getByTestId("agent-model-remove")).toBeDisabled();
    expect(screen.getByTestId("agent-model-remove-refused")).toHaveTextContent("Coding uses this model");
    fireEvent.click(screen.getByTestId("agent-model-remove"));
    expect(latest.catalog["gpt-4.1"]).toBeDefined();

    // An unused entry goes.
    fireEvent.click(screen.getByTestId("agent-model-back"));
    fireEvent.click(screen.getByTestId("agent-model-card-mini"));
    fireEvent.click(screen.getByTestId("agent-model-remove"));
    expect(Object.keys(latest.catalog)).toEqual(["gpt-4.1"]);
  });

  it("RenameEntry_CarriesTheRolesThatNamedIt", async () => {
    render(<Harness />);
    fireEvent.click(screen.getByTestId("agent-model-card-gpt-4.1"));
    fireEvent.change(screen.getByTestId("agent-model-name"), { target: { value: "main" } });
    fireEvent.click(screen.getByTestId("agent-model-apply"));

    expect(Object.keys(latest.catalog).sort()).toEqual(["main", "mini"]);
    expect(latest.models).toEqual({ primary: "main" });
    expect(within(screen.getByTestId("agent-model-card-main")).getByText("used by Coding")).toBeInTheDocument();
  });
});
