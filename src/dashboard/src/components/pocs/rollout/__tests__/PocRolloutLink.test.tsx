import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { PocRow } from "@/lib/pocsApi";
import { PocRolloutLink, pocOfRun } from "../PocRolloutLink";

// 2026-11-23-c81ec: a poc-* run's page links its Rollout page; the PoC is found by its last run, else by its project.

const fetchPocs = vi.fn();
vi.mock("@/lib/pocsApi", () => ({ fetchPocs: (...args: unknown[]) => fetchPocs(...args) }));

function poc(over: Partial<PocRow>): PocRow {
  return {
    id: 1, name: "todo", project: "poc-todo", target: "main", host: null, state: "ready", ownerName: "a person",
    lastRunId: null, latestRevision: 1, ...over,
  };
}

beforeEach(() => fetchPocs.mockReset());

describe("PocRolloutLink", () => {
  it("pocOfRun_PrefersTheLastRun_ThenTheProject", () => {
    const pocs = [poc({ id: 1, project: "poc-a", lastRunId: "r-1" }), poc({ id: 2, project: "poc-b", lastRunId: "r-2" })];
    expect(pocOfRun(pocs, "r-2", "poc-a")?.id).toBe(2);
    expect(pocOfRun(pocs, "r-old", "poc-a")?.id).toBe(1);
    expect(pocOfRun(pocs, "r-old", "poc-c")).toBeUndefined();
  });

  it("PocRolloutLink_PocRun_LinksTheRolloutPage", async () => {
    fetchPocs.mockResolvedValue({ status: "on", list: { pocs: [poc({ id: 4, lastRunId: "r-7" })], mayImport: false, targets: [] } });
    render(<PocRolloutLink runId="r-7" project="poc-todo" />);

    expect(await screen.findByTestId("run-poc-rollout")).toHaveAttribute("href", "/pocs/4/rollouts/r-7");
    expect(screen.getByTestId("run-poc-rollout")).toHaveTextContent("Rollout");
  });

  it("PocRolloutLink_OtherProject_ReadsNothing", () => {
    render(<PocRolloutLink runId="r-7" project="main" />);
    expect(fetchPocs).not.toHaveBeenCalled();
    expect(screen.queryByTestId("run-poc-rollout")).not.toBeInTheDocument();
  });
});
