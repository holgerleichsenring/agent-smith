import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { hold } from "@/components/tickets/__tests__/ticketFixtures";
import { attempt, detail as ticketDetail } from "@/components/tickets/detail/__tests__/detailFixtures";
import { ticketState } from "@/components/tickets/detail/ticketState";
import { yourMove } from "@/components/tickets/detail/yourMove";
import type { TicketHold } from "@/types/tickets";
import { SeriesHoldsSection } from "../SeriesHoldsSection";
import { seriesSectionOf } from "../seriesPageSections";
import { seriesDetail, seriesTicket } from "./seriesFixtures";

// 2026-11-23-31dcg: What holds the rest — each held ticket with whose it is, the hold's sentence and its act; the ones
// that move on by themselves are one line; the next merge is Merge next's and not repeated.

// 2026-11-23-31dcj: the pane also reads Changed in this series (SeriesChangesNotes.test.tsx); here it answers nothing.
vi.mock("@/lib/seriesRunsApi", () => ({ fetchSeriesRunChanges: async () => ({ changes: [], unread: [] }) }));

const edited = hold("text_edited", { act: { kind: "conversation", target: null } });
const startsOnMerge = hold("starts_on_merge", { names: ["#49"], act: { kind: "merge_next", target: "sr-1" } });

const detail = () => seriesDetail({
  counts: { total: 5, landed: 0, waiting: 1, held: 2, notStarted: 1 },
  tickets: [
    seriesTicket({ ticketId: "49", key: "#49", title: "Domain", hold: hold("merge_next", { act: { kind: "merge", target: "sr-1" } }) }),
    seriesTicket({ ticketId: "61", key: "#61", title: "Callbacks", hold: edited, isHeld: true }),
    seriesTicket({ ticketId: "58", key: "#58", title: "Admin dashboard", hold: startsOnMerge, isHeld: true }),
    seriesTicket({ ticketId: "62", key: "#62", title: "Sources", hold: hold("working") }),
    seriesTicket({ ticketId: "63", key: "#63", title: "Docs", hold: hold("prerequisite", { names: ["#62"] }) }),
  ],
});

describe("SeriesHoldsSection", () => {
  it("SeriesHolds_ListsEachHeldTicket_WithWhoseItIs_ItsSentenceAndAct", () => {
    render(<SeriesHoldsSection detail={detail()} />);

    expect(screen.queryByTestId("series-hold-49")).toBeNull();
    expect(screen.getByTestId("series-hold-61")).toHaveTextContent("plan gap");
    expect(screen.getByTestId("series-hold-sentence-61")).toHaveTextContent("Settle it in the conversation.");
    expect(screen.getByTestId("series-hold-act-61")).toHaveAttribute("href", "/tickets/shop/61");
    expect(screen.getByTestId("series-hold-act-61")).toHaveTextContent("Discuss in the conversation");
    expect(screen.getByTestId("series-hold-sentence-58")).toHaveTextContent("Starts once #49 is merged.");
    expect(screen.getByTestId("series-hold-act-58")).toHaveAttribute("href", "/series/sr-1?section=next");
    expect(screen.getByTestId("series-holds-moving")).toHaveTextContent("2 more");
  });

  // 2026-11-23-d60d: red CI on a series ticket's pull request is a decision whose act opens the pull request.
  it("SeriesHolds_RedCi_NamesTheFailedCheck_AndOpensThePullRequest", () => {
    const red = hold("ci_failed", { names: ["build"], act: { kind: "pull_request", target: "https://example.test/pr/64" } });
    render(<SeriesHoldsSection detail={seriesDetail({ tickets: [seriesTicket({ ticketId: "64", hold: red, isHeld: true })] })} />);
    expect(screen.getByTestId("series-hold-sentence-64")).toHaveTextContent("CI failed: build.");
    expect(screen.getByTestId("series-hold-act-64")).toHaveAttribute("href", "https://example.test/pr/64");
    expect(screen.getByTestId("series-hold-act-64")).toHaveTextContent("Open the pull request");
  });

  it("SeriesHolds_TheSentence_IsTheTicketBoxsText_ForAPlanAndForAnAdminStopWithItsDetail", () => {
    const endDetail = { subject: "Image node:22", repos: ["web"], control: "Trusted image registries", setTo: "ghcr.io/", settingsPath: "/x" };
    const image = hold("image_untrusted", { act: { kind: "settings", target: "/x" }, detail: endDetail });
    render(<SeriesHoldsSection detail={seriesDetail({ tickets: [
      seriesTicket({ ticketId: "61", hold: edited, isHeld: true }), seriesTicket({ ticketId: "70", hold: image, isHeld: true })] })} />);
    const box = (ticketId: string, h: TicketHold) => {
      const page = ticketDetail([attempt({ status: "failed", endKind: "image-untrusted", endDetail })]);
      page.summary = { ...page.summary, ticketId, bucket: "needs_you", hold: h };
      const view = ticketState({ detail: page, liveEvents: [], series: null })!;
      return yourMove(view, page.summary.bucket, view.attempt?.endDetail).text;
    };

    expect(screen.getByTestId("series-hold-sentence-61").textContent).toBe(box("61", edited));
    expect(screen.getByTestId("series-hold-sentence-70").textContent).toBe(box("70", image));
  });

  it("SeriesHolds_NothingHeld_SaysSo", () => {
    render(<SeriesHoldsSection detail={seriesDetail()} />);
    expect(screen.getByTestId("series-holds-empty")).toBeInTheDocument();
  });

  it("SeriesHolds_IsListed_CountedByTheServersHeldCount", () => {
    const section = seriesSectionOf("held");
    expect(section.id).toBe("held");
    expect(section.counter(detail())).toEqual({ text: "2", hot: true });
  });
});
