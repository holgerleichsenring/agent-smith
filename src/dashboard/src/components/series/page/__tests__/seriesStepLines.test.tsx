import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { hold } from "@/components/tickets/__tests__/ticketFixtures";
import { translator } from "@/lib/i18n/translate";
import { SeriesYourStep } from "../SeriesYourStep";
import { mergesNow, seriesMergeLine, seriesStepLines } from "../seriesStepLines";
import { seriesDetail, seriesTicket } from "./seriesFixtures";

// 2026-11-23-849e: Your next step — one line per person's hold with its act, and whether merging is what moves the rest.

const en = translator("en");
const pr = { repo: "server", number: "9934", url: "u", state: "open", ci: "green", ciFailure: null, ciLog: null, target: null };

const series = () => seriesDetail({
  tickets: [
    seriesTicket({ ticketId: "19648", key: "#19648", pullRequests: [pr],
      hold: hold("merge_next", { next: "#19649", names: ["#19662", "#19665"], act: { kind: "merge", target: "sr-1" } }) }),
    seriesTicket({ ticketId: "19649", key: "#19649", hold: hold("merge_after", { names: ["#19648"] }) }),
    seriesTicket({ ticketId: "19650", key: "#19650", hold: hold("question", { act: { kind: "answer", target: null } }) }),
    seriesTicket({ ticketId: "19662", key: "#19662", hold: hold("starts_on_merge", { names: ["#19653"], merges: 5, next: "#19648" }) }),
    seriesTicket({ ticketId: "19665", key: "#19665", hold: hold("starts_on_merge", { names: ["#19653"], merges: 5, next: "#19648" }) }),
    seriesTicket({ ticketId: "19600", key: "#19600", landed: true }),
  ],
  mergeOrder: [{ ticketId: "19648", key: "#19648", depth: 0, wait: "ready", after: [] },
    { ticketId: "19649", key: "#19649", depth: 1, wait: "after", after: ["#19648"] }],
});

describe("seriesStepLines", () => {
  it("seriesStepLines_TheMergeSaysWhatItUnblocks_AQuestionOpensTheTicket", () => {
    const lines = seriesStepLines(series(), "/series/sr-1", en);

    expect(lines.map((l) => l.ticketId)).toEqual(["19648", "19650"]);
    // 2026-11-23-8d16: the merge's line is the merge act; which tickets wait on merges is the summary line's.
    expect(lines[0].text).toBe("Review and merge PR #9934 (#19648). #19649 is next after it.");
    expect(lines[0].merge).toBe(true);
    expect(lines[1].act.href).toBe("/tickets/shop/19650");
  });

  it("seriesMergeLine_CountsWhatGoesOnWithoutMerging_AndNamesWhatNeedsMerges", () => {
    // #19648's pull request is the merge itself; #19649 (merge_after, no pull request in the fixture) and the question go on.
    expect(seriesMergeLine(series(), en)).toBe("2 tickets continue without merging; 2 need merges to start (#19662 and #19665).");
    expect(seriesMergeLine(seriesDetail({ tickets: [seriesTicket({ hold: hold("working") })] }), translator("de")))
      .toBe("1 Ticket geht ohne Merge weiter; keines braucht einen Merge, um zu starten.");
    expect(seriesMergeLine(seriesDetail({ tickets: [seriesTicket({ landed: true })] }), en)).toBeNull();
  });

  // 2026-11-23-8d16: the merges the server marked are counted and named, in both languages.
  it("seriesMergeLine_CountsTheMarkedMerges_TheWaitingTicketsNeed", () => {
    const d = series();
    d.mergeOrder = d.mergeOrder.map((s) => ({ ...s, neededBy: ["#19662", "#19665"] }));

    expect(seriesMergeLine(d, en)).toBe(
      "2 tickets continue without merging; 2 merges are needed before #19662 and #19665 can start (marked under Merge next).");
    expect(seriesMergeLine(d, translator("de"))).toContain(
      "2 Merges sind nötig, damit #19662 und #19665 starten können (markiert unter Als Nächstes mergen)");
  });

  it("mergesNow_OnlyWhileTheHoldIsTheMerge", () => {
    const d = series();
    expect(mergesNow(d, d.mergeOrder[0])).toBe(true);
    expect(mergesNow(d, d.mergeOrder[1])).toBe(false);
    d.tickets[0] = { ...d.tickets[0], hold: hold("ci_running") };
    expect(mergesNow(d, d.mergeOrder[0])).toBe(false);
  });

  it("SeriesYourStep_DrawsTheLines_OrSaysThereIsNothingToDo", () => {
    const { unmount } = render(<SeriesYourStep detail={series()} />);
    expect(screen.getByTestId("series-your-step")).toHaveTextContent("Your next step");
    expect(screen.getByTestId("series-your-step-19648")).toHaveTextContent("Review and merge PR #9934 (#19648).");
    expect(screen.getByTestId("series-your-step-merges")).toHaveTextContent("2 need merges to start");
    unmount();

    render(<SeriesYourStep detail={seriesDetail({ tickets: [seriesTicket({ hold: hold("working") })] })} />);
    expect(screen.getByTestId("series-your-step-nothing")).toHaveTextContent("Nothing for you to do now.");
  });
});
