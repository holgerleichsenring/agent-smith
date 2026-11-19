import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { DialogSeriesPanel } from "../DialogSeriesPanel";
import type { SeriesRunView } from "@/types/series";

// 2026-11-17-0207e: the series panel reads like the mock — progress, spend, the estimated finish, every ticket (1b72a),
// the critical path and the policy.

const series: SeriesRunView = {
  id: "sr-1", state: "running", headState: "series running · 1 question for you", landed: 6, total: 25,
  elapsed: "1 h 03 min", cost: "$96.0", eta: "22:50 UTC",
  tickets: [
    { ticket: "SCRUM-43", what: "Schema", state: "landed", kind: "landed", cost: "$4.0", time: "22m" },
    { ticket: "SCRUM-44", what: "Forwarding", state: "coding", kind: "running", cost: "$2.5", time: "12m" },
    { ticket: "SCRUM-46", what: "Notices", state: "red · a test expects the old schema", kind: "red", cost: "$3.0", time: "30m" },
    { ticket: "SCRUM-47", what: "Docs", state: "waits on SCRUM-44", kind: "held", cost: "–", time: "–" },
  ],
  criticalPath: ["SCRUM-43", "SCRUM-44", "SCRUM-47"], criticalPathLeft: "3 tickets, about 2 h 10 min left",
  policy: "merge on green CI · start on green push · questions default to the recommendation after 2 h 00 min",
  question: "Keep the original deadline?",
};

describe("DialogSeriesPanel", () => {
  it("SeriesPanel_RunningSeries_ShowsProgressNowPathAndPolicy", () => {
    render(<DialogSeriesPanel series={series} />);

    expect(screen.getByTestId("dialog-series-progress")).toHaveTextContent("6 of 25 landed · 1 h 03 min");
    expect(screen.getByTestId("dialog-series-cost")).toHaveTextContent("$96.0 spent · est. finish 22:50 UTC");
    expect(screen.getByTestId("dialog-series-tickets").querySelectorAll("li")).toHaveLength(4);
    expect(screen.getByTestId("dialog-series-path")).toHaveTextContent("SCRUM-43 → SCRUM-44 → SCRUM-47");
    expect(screen.getByTestId("dialog-series-policy")).toHaveTextContent("merge on green CI");
  });

  it("SeriesPanel_EveryTicket_IsARowWithKeyTitleStateCostAndTime", () => {
    render(<DialogSeriesPanel series={series} />);

    const red = screen.getByTestId("dialog-series-tickets").querySelectorAll("li")[2];
    expect(red).toHaveAttribute("data-kind", "red");
    expect(red).toHaveTextContent("SCRUM-46Noticesred · a test expects the old schema$3.030m");
  });

  it("SeriesPanel_NoMeasureYet_SaysNoFinish", () => {
    render(<DialogSeriesPanel series={{ ...series, eta: null, criticalPathLeft: null }} />);

    expect(screen.getByTestId("dialog-series-cost")).toHaveTextContent(/^\$96\.0 spent$/);
  });
});
