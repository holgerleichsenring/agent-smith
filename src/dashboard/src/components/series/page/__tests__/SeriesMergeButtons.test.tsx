import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { SeriesRunMergeStep, SeriesRunPullRequest } from "@/types/seriesRuns";
import { hold } from "@/components/tickets/__tests__/ticketFixtures";
import { seriesDetail, seriesTicket } from "./seriesFixtures";

// 2026-11-23-8d16: every button labelled Merge on the series page posts the merge — Your next step's as well as the row's —
// and none navigates; Your next step's is enabled exactly when the row's is; every row and the next-step line carry an Open
// pull request button per open pull request, also where the merge is not next; the merges others start on are marked; a
// draft says so.

const hoisted = vi.hoisted(() => ({
  merge: vi.fn(), fetch: vi.fn(), changes: vi.fn(), replace: vi.fn(), push: vi.fn(), lacks: false, search: "",
}));
vi.mock("@/lib/seriesRunsApi", () => ({
  mergeSeriesTicket: (...a: unknown[]) => hoisted.merge(...a),
  fetchSeriesRun: (...a: unknown[]) => hoisted.fetch(...a),
  fetchSeriesRunChanges: (...a: unknown[]) => hoisted.changes(...a),
}));
vi.mock("@/hooks/useLacksPermission", () => ({ useLacksPermission: () => hoisted.lacks }));
vi.mock("next/navigation", () => ({
  usePathname: () => "/series/sr-1",
  useSearchParams: () => new URLSearchParams(hoisted.search),
  useRouter: () => ({ replace: hoisted.replace, push: hoisted.push }),
}));

import { SeriesRunPage } from "../SeriesRunPage";
import { SeriesYourStep } from "../SeriesYourStep";

const pr = (over: Partial<SeriesRunPullRequest> = {}): SeriesRunPullRequest => ({
  repo: "server", number: "9935", url: "https://git/server/pull/9935", state: "open", ci: "green", ciFailure: null, ciLog: null,
  target: null, ...over,
});

const step = (ticketId: string, wait: SeriesRunMergeStep["wait"], over: Partial<SeriesRunMergeStep> = {}): SeriesRunMergeStep =>
  ({ ticketId, key: `#${ticketId}`, depth: 0, wait, after: [], ...over });

const detail = () => seriesDetail({
  tickets: [
    seriesTicket({ ticketId: "49", key: "#49", pullRequests: [pr({ isDraft: true })],
      hold: hold("merge_next", { next: "#50", act: { kind: "merge", target: "sr-1" } }) }),
    seriesTicket({ ticketId: "50", key: "#50", pullRequests: [pr({ number: "9940", url: "https://git/server/pull/9940" })],
      hold: hold("merge_after", { names: ["#49"] }) }),
    seriesTicket({ ticketId: "52", key: "#52", hold: hold("starts_on_merge", { names: ["#50"], merges: 2, next: "#49" }) }),
  ],
  mergeOrder: [step("49", "ready", { neededBy: ["#52"] }), step("50", "after", { after: ["#49"], neededBy: ["#52"] })],
});

beforeEach(() => {
  hoisted.merge.mockReset().mockResolvedValue({ kind: "said", text: "#49 was not merged: another run holds this ticket." });
  hoisted.fetch.mockReset().mockResolvedValue(detail());
  hoisted.changes.mockReset().mockResolvedValue({ changes: [], unread: [] });
  hoisted.replace.mockReset();
  hoisted.push.mockReset();
  hoisted.lacks = false;
  hoisted.search = "";
});

describe("series merge buttons", () => {
  it("SeriesPage_EveryButtonLabelledMerge_PostsTheMerge_AndNoneNavigates", async () => {
    render(<SeriesRunPage id="sr-1" />);
    await screen.findByTestId("series-pane-next");

    const merges = screen.getAllByRole("button", { name: "Merge" }).filter((b) => !(b as HTMLButtonElement).disabled);
    expect(merges).toHaveLength(2); // Your next step's and the ready row's
    expect(screen.queryAllByRole("link", { name: "Merge" })).toHaveLength(0);
    for (const button of merges) {
      fireEvent.click(button);
      await waitFor(() => expect(hoisted.merge).toHaveBeenLastCalledWith("sr-1", "49"));
      await waitFor(() => expect(button).not.toHaveTextContent("Merging"));
    }
    expect(hoisted.merge).toHaveBeenCalledTimes(2);
    expect(hoisted.push).not.toHaveBeenCalled();
    expect(hoisted.replace).not.toHaveBeenCalled();
  });

  it("SeriesPage_AMergeStartedInYourNextStep_DisablesTheRowsMerge_TillItAnswers", async () => {
    let answer: (value: unknown) => void = () => {};
    hoisted.merge.mockReturnValue(new Promise((resolve) => { answer = resolve; }));
    render(<SeriesRunPage id="sr-1" />);
    await screen.findByTestId("series-pane-next");

    fireEvent.click(screen.getByTestId("series-your-step-button-49"));

    expect(screen.getByTestId("series-merge-button-49")).toBeDisabled();
    answer({ kind: "said", text: "#49 was not merged: another run holds this ticket." });
    await waitFor(() => expect(screen.getByTestId("series-merge-49")).toHaveTextContent("another run holds this ticket"));
  });

  it("YourStep_Merge_IsEnabledExactlyWhenTheRowsIs_AndShowsTheRefusal", async () => {
    const running = detail();
    running.tickets[0] = { ...running.tickets[0], hold: hold("ci_running") };
    const { unmount } = render(<SeriesYourStep detail={running} />);
    expect(screen.queryByTestId("series-your-step-button-49")).toBeNull(); // no merge hold, no merge line
    unmount();

    const notReady = detail();
    notReady.mergeOrder = [step("49", "after", { after: ["#48"] })];
    const second = render(<SeriesYourStep detail={notReady} />);
    expect(screen.getByTestId("series-your-step-button-49")).toBeDisabled();
    second.unmount();

    render(<SeriesYourStep detail={detail()} />);
    fireEvent.click(screen.getByTestId("series-your-step-button-49"));
    expect(await screen.findByTestId("series-merge-said")).toHaveTextContent("another run holds this ticket");
  });

  it("OpenPullRequest_OnEveryRowAndTheNextStep_OpensTheUrlInANewTab", async () => {
    hoisted.lacks = true;
    render(<SeriesRunPage id="sr-1" />);
    await screen.findByTestId("series-pane-next");

    const later = screen.getByTestId("series-merge-open-50-server");
    expect(later).toHaveAttribute("href", "https://git/server/pull/9940");
    expect(later).toHaveAttribute("target", "_blank");
    expect(later).toHaveTextContent("Open pull request #9940");
    expect(screen.getByTestId("series-your-step-open-49-server")).toHaveAttribute("href", "https://git/server/pull/9935");
    expect(screen.queryAllByRole("button", { name: "Merge" })).toHaveLength(0); // without runs.control
  });

  it("MergeNext_MarksTheNeededMerges_AndADraft", async () => {
    render(<SeriesRunPage id="sr-1" />);
    await screen.findByTestId("series-pane-next");

    expect(screen.getByTestId("series-merge-49")).toHaveClass("needed");
    expect(screen.getByTestId("series-merge-needed-50")).toHaveTextContent("Needed so #52 can start.");
    expect(screen.getByTestId("series-merge-draft-49-server")).toHaveTextContent("Draft");
    expect(screen.getByTestId("series-merge-why-49")).toHaveTextContent("merging takes it out of draft first");
    expect(within(screen.getByTestId("series-your-step-merges")).getByText(/2 merges are needed before #52 can start/)).toBeInTheDocument();
  });
});
