import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { DialogSeriesPanel } from "../DialogSeriesPanel";
import type { SeriesRunView } from "@/types/series";

// 2026-11-17-0207e: the series panel reads like the mock — progress, spend, the estimated finish, what moves now,
// the critical path and the policy.

const series: SeriesRunView = {
  id: "sr-1", state: "running", headState: "series running · 1 question for you", landed: 6, total: 25,
  elapsed: "1 h 03 min", cost: "$96.0", eta: "22:50 UTC",
  now: [
    { ticket: "SCRUM-44", text: "coding", kind: "running" },
    { ticket: "SCRUM-46", text: "red · a test expects the old schema", kind: "red" },
    { ticket: "SCRUM-47", text: "waits on SCRUM-44", kind: "held" },
  ],
  criticalPath: ["SCRUM-43", "SCRUM-44", "SCRUM-47"], criticalPathLeft: "3 tickets, about 2 h 10 min left",
  policy: "merge on green CI · start on green push · status every 15 min · questions default to the recommendation after 2 h 00 min",
  question: "Keep the original deadline?",
};

describe("DialogSeriesPanel", () => {
  it("SeriesPanel_RunningSeries_ShowsProgressNowPathAndPolicy", () => {
    render(<DialogSeriesPanel series={series} />);

    expect(screen.getByTestId("dialog-series-progress")).toHaveTextContent("6 of 25 landed · 1 h 03 min");
    expect(screen.getByTestId("dialog-series-cost")).toHaveTextContent("$96.0 spent · est. finish 22:50 UTC");
    expect(screen.getByTestId("dialog-series-now").querySelectorAll("li")).toHaveLength(3);
    expect(screen.getByTestId("dialog-series-path")).toHaveTextContent("SCRUM-43 → SCRUM-44 → SCRUM-47");
    expect(screen.getByTestId("dialog-series-policy")).toHaveTextContent("merge on green CI");
  });

  it("SeriesPanel_NoMeasureYet_SaysNoFinish", () => {
    render(<DialogSeriesPanel series={{ ...series, eta: null, criticalPathLeft: null }} />);

    expect(screen.getByTestId("dialog-series-cost")).toHaveTextContent(/^\$96\.0 spent$/);
  });
});
