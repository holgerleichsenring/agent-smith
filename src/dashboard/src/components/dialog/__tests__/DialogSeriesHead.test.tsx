import { describe, it, expect, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { DialogSeriesHead } from "../DialogSeriesHead";
import type { SeriesRunView } from "@/types/series";

// 2026-11-19-1b72a: the conversation head shows the series' state with Pause while it runs and Resume while it is
// paused — the act the series master takes for the same words.

const series: SeriesRunView = {
  id: "sr-1", state: "running", headState: "series running", landed: 2, total: 25, elapsed: "40m", cost: "$12.0",
  eta: null, tickets: [], criticalPath: [], criticalPathLeft: null, policy: "merge by hand", question: null,
};

describe("DialogSeriesHead", () => {
  it("DialogSeriesHead_Pause_SendsThePause", async () => {
    const onAct = vi.fn(async () => {});
    render(<DialogSeriesHead series={series} onAct={onAct} />);

    fireEvent.click(screen.getByTestId("dialog-series-act"));

    expect(onAct).toHaveBeenCalledWith("pause");
    expect(screen.getByTestId("dialog-series-state")).toHaveTextContent("series running");
    await waitFor(() => expect(screen.getByTestId("dialog-series-act")).not.toBeDisabled());
  });

  it("DialogSeriesHead_Paused_OffersResume", async () => {
    const onAct = vi.fn(async () => {});
    render(<DialogSeriesHead series={{ ...series, state: "paused", headState: "series paused" }} onAct={onAct} />);

    const button = screen.getByTestId("dialog-series-act");
    expect(button).toHaveTextContent("Resume");
    fireEvent.click(button);
    expect(onAct).toHaveBeenCalledWith("resume");
    await waitFor(() => expect(button).not.toBeDisabled());
  });

  it("DialogSeriesHead_EndedSeries_HasNoButton", () => {
    render(<DialogSeriesHead series={{ ...series, state: "done", headState: "series done" }} onAct={vi.fn()} />);

    expect(screen.queryByTestId("dialog-series-act")).toBeNull();
  });

  it("DialogSeriesHead_Refused_SaysWhy", async () => {
    const onAct = vi.fn(async () => { throw new Error("This conversation has no running series."); });
    render(<DialogSeriesHead series={series} onAct={onAct} />);

    fireEvent.click(screen.getByTestId("dialog-series-act"));

    await waitFor(() => expect(screen.getByRole("alert")).toHaveTextContent("This conversation has no running series."));
  });
});
