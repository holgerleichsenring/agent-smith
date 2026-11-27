import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import type { SeriesRunPullRequest } from "@/types/seriesRuns";
import { SeriesRepoChips } from "../SeriesRepoChips";
import { seriesRepoChipLabels } from "../seriesRepoLabels";
import { seriesTicket } from "./seriesFixtures";

// 2026-11-27-ecf2b: a chip per pull request — a short label unique within the series, a link to the pull request in a
// new tab, filled for code and outlined for a pull request carrying only the spec record.

const pr = (repo: string, number: string, recordOnly = false): SeriesRunPullRequest => ({
  repo, number, url: `https://git.test/${repo}/pull/${number}`, state: "open", ci: null, ciFailure: null, ciLog: null, target: null, recordOnly,
});

describe("seriesRepoChipLabels", () => {
  it("labels are one letter of the name's last part, longer only where two repos would clash", () => {
    expect(seriesRepoChipLabels(["notify.Worker", "notify.Client", "notify.Server"])).toEqual(
      { "notify.Worker": "W", "notify.Client": "C", "notify.Server": "S" });
    expect(seriesRepoChipLabels(["shop-server", "shop-scheduler", "shop-web"])).toEqual(
      { "shop-server": "Se", "shop-scheduler": "Sc", "shop-web": "W" });
    expect(Object.values(seriesRepoChipLabels(["a/server", "b/server"]))).toEqual(["Server1", "Server2"]);
    expect(seriesRepoChipLabels(["shop-web", "shop-webapp"])).toEqual({ "shop-web": "Web", "shop-webapp": "Weba" });
  });
});

describe("SeriesRepoChips", () => {
  it("repo chips are unique within the series and outline a record-only pull request", () => {
    const ticket = seriesTicket({ ticketId: "7", pullRequests: [pr("worker", "1", true), pr("client", "2", true), pr("server", "3")] });
    render(<SeriesRepoChips ticket={ticket} labels={seriesRepoChipLabels(["worker", "client", "server"])} />);

    const server = screen.getByTestId("series-node-chip-server-7");
    expect(server).toHaveClass("code");
    expect(server).toHaveTextContent("S");
    expect(server).toHaveAttribute("href", "https://git.test/server/pull/3");
    expect(server).toHaveAttribute("target", "_blank");
    expect(server).toHaveAttribute("title", "server PR #3 · code");
    const worker = screen.getByTestId("series-node-chip-worker-7");
    expect(worker).not.toHaveClass("code");
    expect(worker).toHaveAttribute("title", "worker PR #1 · spec record only");
  });

  it("a ticket without pull requests has no chips", () => {
    const { container } = render(<SeriesRepoChips ticket={seriesTicket()} labels={{}} />);

    expect(container).toBeEmptyDOMElement();
  });
});
