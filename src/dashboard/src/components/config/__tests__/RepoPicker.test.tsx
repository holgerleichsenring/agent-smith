import { describe, it, expect, vi, beforeEach } from "vitest";
import { useState } from "react";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { render, screen, fireEvent } from "@testing-library/react";
import { RepoPicker } from "../RepoPicker";
import { fetchConnectionRepos, type DiscoveredRepo, type StudioEntity } from "@/lib/configApi";

// p0488: the picker in front of a connection with many repos — filter, capped
// window, and the wildcard read as the RULE it is rather than one more chip.

vi.mock("@/lib/configApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/configApi")>()),
  fetchConnectionRepos: vi.fn(),
}));

const mockedRepos = vi.mocked(fetchConnectionRepos);
const connections: StudioEntity[] = [{ id: "conn" }];

const three: DiscoveredRepo[] = [
  { name: "Sample.Api", defaultBranch: "main" },
  { name: "Sample.Web", defaultBranch: null },
  { name: "Other.Service", defaultBranch: "trunk" },
];
const many: DiscoveredRepo[] = Array.from({ length: 60 }, (_, i) => ({
  name: `Repo.${String(i + 1).padStart(2, "0")}`,
  defaultBranch: "main",
}));

function discovers(repos: DiscoveredRepo[], discoveredAt: string | null = "2026-08-19T09:00:00Z") {
  mockedRepos.mockResolvedValue({ discoveredAt, repos });
}

function Picker({ initial = [] }: { initial?: string[] }) {
  const [values, setValues] = useState<string[]>(initial);
  return (
    <RepoPicker label="connection-scoped repos" values={values} connections={connections} onChange={setValues} />
  );
}

/** Render, pick the one connection, and wait for its discovery snapshot to land. */
async function pickConnection(initial: string[] = []) {
  render(<Picker initial={initial} />);
  fireEvent.change(screen.getByTestId("form-connref-connection"), { target: { value: "conn" } });
  await screen.findByTestId("form-connref-count");
}

const filter = (text: string) =>
  fireEvent.change(screen.getByTestId("form-connref-filter"), { target: { value: text } });
const rows = () => screen.queryAllByTestId(/^form-connref-row-/);

beforeEach(() => {
  mockedRepos.mockReset();
  discovers(three);
});

describe("RepoPicker (p0488)", () => {
  it("RepoPicker_Filter_NarrowsTheList_AndStatesMatchedOfTotal", async () => {
    await pickConnection();
    expect(rows()).toHaveLength(3);
    expect(screen.getByTestId("form-connref-count")).toHaveTextContent("showing 3 of 3 matched · 3 discovered");

    filter("Sample");

    expect(rows().map((r) => r.getAttribute("data-testid"))).toEqual([
      "form-connref-row-Sample.Api",
      "form-connref-row-Sample.Web",
    ]);
    expect(screen.queryByTestId("form-connref-row-Other.Service")).toBeNull();
    // The count of matched-of-total is stated, always — nothing vanishes silently.
    expect(screen.getByTestId("form-connref-count")).toHaveTextContent("showing 2 of 2 matched · 3 discovered");
  });

  it("RepoPicker_ManyRepos_RendersACappedWindow_AndSaysHowManyAreHidden", async () => {
    discovers(many);
    await pickConnection();

    expect(rows()).toHaveLength(25);
    expect(screen.getByTestId("form-connref-count")).toHaveTextContent(
      "showing 25 of 60 matched · 60 discovered · 35 hidden",
    );
    expect(screen.getByTestId("form-connref-more")).toHaveTextContent("show 25 more");
    // Capped, not paged: no page controls, and the tail is named, not dropped.
    expect(screen.queryByTestId("form-connref-row-Repo.60")).toBeNull();
  });

  it("RepoPicker_ShowMore_RevealsTheNextWindow", async () => {
    discovers(many);
    await pickConnection();

    fireEvent.click(screen.getByTestId("form-connref-more"));
    expect(rows()).toHaveLength(50);
    expect(screen.getByTestId("form-connref-count")).toHaveTextContent("showing 50 of 60 matched");
    expect(screen.getByTestId("form-connref-more")).toHaveTextContent("show 10 more");

    fireEvent.click(screen.getByTestId("form-connref-more"));
    expect(rows()).toHaveLength(60);
    expect(screen.getByTestId("form-connref-count")).toHaveTextContent("showing 60 of 60 matched");
    expect(screen.queryByTestId("form-connref-more")).toBeNull();
  });

  it("RepoPicker_FilterMatchesNothing_SaysSo_WithoutLosingSelection", async () => {
    await pickConnection();
    fireEvent.click(screen.getByTestId("form-connref-discovered-Sample.Api"));
    expect(screen.getByTestId("form-connref-chip-conn/Sample.Api")).toBeInTheDocument();

    filter("zzz");

    // The fifth state — "the filter matched nothing" is not "nothing was discovered".
    expect(screen.getByTestId("form-connref-nomatch")).toHaveTextContent("no discovered repo matches");
    expect(screen.queryByTestId("form-connref-none")).toBeNull();
    expect(screen.getByTestId("form-connref-count")).toHaveTextContent("showing 0 of 0 matched · 3 discovered");
    // …and what was already picked is still picked.
    expect(screen.getByTestId("form-connref-chip-conn/Sample.Api")).toBeInTheDocument();
  });

  it("RepoPicker_WildcardChip_CarriesItsLiveMatchCount", async () => {
    await pickConnection(["conn/Sample.*", "conn/Other.Service"]);

    const rule = screen.getByTestId("form-connref-chip-conn/Sample.*");
    expect(rule).toHaveAttribute("data-kind", "rule");
    expect(screen.getByTestId("form-connref-rulecount-conn/Sample.*")).toHaveTextContent("2 match");
    // An exact ref is a pick — it names one repo and carries no count.
    expect(screen.getByTestId("form-connref-chip-conn/Other.Service")).toHaveAttribute("data-kind", "pick");
    expect(screen.queryByTestId("form-connref-rulecount-conn/Other.Service")).toBeNull();
  });

  it("RepoPicker_RepoCoveredByARule_RendersAsCovered_NotAsAFreeCheckbox", async () => {
    await pickConnection(["conn/Sample.*"]);

    const covered = screen.getByTestId("form-connref-row-Sample.Api");
    expect(covered).toHaveAttribute("data-state", "covered");
    expect(screen.getByTestId("form-connref-covered-Sample.Api")).toHaveTextContent("covered by conn/Sample.*");
    // No checkbox: ticking one would add a redundant exact ref shadowing the rule.
    expect(screen.queryByTestId("form-connref-discovered-Sample.Api")).toBeNull();

    // A repo the rule does NOT cover stays freely selectable.
    expect(screen.getByTestId("form-connref-row-Other.Service")).toHaveAttribute("data-state", "free");
    expect(screen.getByTestId("form-connref-discovered-Other.Service")).toBeInTheDocument();
  });

  it("RepoPicker_FilterContainingAStar_OffersTheRule_FromTheSameBox", async () => {
    await pickConnection();
    // There is no second "name or wildcard" input — the filter box is the one box.
    expect(screen.queryByTestId("form-connref-name")).toBeNull();

    filter("Sample.*");

    const add = screen.getByTestId("form-connref-add");
    expect(add).toHaveTextContent("add rule conn/Sample.* · 2 match");
    fireEvent.click(add);

    expect(screen.getByTestId("form-connref-chip-conn/Sample.*")).toBeInTheDocument();
    expect(screen.queryByTestId("form-connref-chip-conn/Sample.Api")).toBeNull();
    // The box empties again, so the whole inventory is back in view.
    expect(screen.getByTestId("form-connref-filter")).toHaveValue("");
  });

  it("RepoPicker_SelectAllFiltered_PrefersTheRule_OverNExactRefs", async () => {
    await pickConnection();

    filter("Sample.*");
    const all = screen.getByTestId("form-connref-select-all");
    expect(all).toHaveTextContent("select all 2 as rule conn/Sample.*");
    fireEvent.click(all);

    expect(screen.getByTestId("form-connref-chip-conn/Sample.*")).toBeInTheDocument();
    expect(screen.queryByTestId("form-connref-chip-conn/Sample.Api")).toBeNull();
    expect(screen.queryByTestId("form-connref-chip-conn/Sample.Web")).toBeNull();
  });

  it("RepoPicker_SelectAllPlainFilter_TakesTheExactRefs", async () => {
    await pickConnection();

    filter("Sample");
    expect(screen.getByTestId("form-connref-select-all")).toHaveTextContent("select all 2");
    fireEvent.click(screen.getByTestId("form-connref-select-all"));

    // Plain text names no rule, so the picks are what gets taken.
    expect(screen.getByTestId("form-connref-chip-conn/Sample.Api")).toBeInTheDocument();
    expect(screen.getByTestId("form-connref-chip-conn/Sample.Web")).toBeInTheDocument();
  });

  it("RepoPicker_TheFourDiscoveryStates_ReadUnchanged", async () => {
    // p0345c's four states are load-bearing; p0488 leaves every word of them alone.
    mockedRepos.mockReturnValue(new Promise(() => {}));
    const loading = render(<Picker />);
    fireEvent.change(screen.getByTestId("form-connref-connection"), { target: { value: "conn" } });
    expect(await screen.findByTestId("form-connref-loading")).toHaveTextContent("loading discovery cache…");
    loading.unmount();

    mockedRepos.mockRejectedValue(new Error("cache down"));
    const failed = render(<Picker />);
    fireEvent.change(screen.getByTestId("form-connref-connection"), { target: { value: "conn" } });
    expect(await screen.findByTestId("form-connref-error")).toHaveTextContent(
      "discovery cache unavailable: cache down",
    );
    failed.unmount();

    discovers([], null);
    const cold = render(<Picker />);
    fireEvent.change(screen.getByTestId("form-connref-connection"), { target: { value: "conn" } });
    expect(await screen.findByTestId("form-connref-undiscovered")).toHaveTextContent(
      "not discovered yet — run a discovery or type a name below",
    );
    // The fallback that state promises still works from the same one box.
    fireEvent.change(screen.getByTestId("form-connref-filter"), { target: { value: "Sample.Api" } });
    fireEvent.click(screen.getByTestId("form-connref-add"));
    expect(screen.getByTestId("form-connref-chip-conn/Sample.Api")).toBeInTheDocument();
    cold.unmount();

    discovers([]);
    render(<Picker />);
    fireEvent.change(screen.getByTestId("form-connref-connection"), { target: { value: "conn" } });
    expect(await screen.findByTestId("form-connref-none")).toHaveTextContent(
      "discovery ran but found no repos in this connection",
    );
    expect(screen.queryByTestId("form-connref-nomatch")).toBeNull();
  });

  it("RefMatches_HasOneImplementation_SharedByInventoryAndPicker", () => {
    const read = (rel: string) => readFileSync(fileURLToPath(new URL(rel, import.meta.url)), "utf8");
    const picker = read("../RepoPicker.tsx");
    const inventory = read("../RepoInventory.tsx");

    for (const [name, source] of [
      ["RepoPicker", picker],
      ["RepoInventory", inventory],
    ] as const) {
      expect(source, `${name} imports the shared glob rule`).toContain('from "@/lib/repoRefs"');
      expect(source, `${name} declares no refMatches of its own`).not.toContain("function refMatches");
    }
    expect(read("../../../lib/repoRefs.ts")).toContain("export function refMatches");
  });
});
