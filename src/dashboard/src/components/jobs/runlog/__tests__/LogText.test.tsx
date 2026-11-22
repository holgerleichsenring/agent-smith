import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { decisionParts } from "../decisionParts";
import { BlockText, InlineText, MasterReport } from "../LogText";
import { DecisionLine } from "../RunLogSectionBodies";

// 2026-11-18-e06bb: log text renders its markdown; a decision is one line, bold title, dim reason, full on click.

describe("LogText", () => {
  it("InlineText_BoldAndCode_RenderWithoutMarkers", () => {
    const { container } = render(<div data-testid="host"><InlineText text={"**Bump** the `api` packages"} /></div>);
    expect(screen.getByTestId("host")).toHaveTextContent("Bump the api packages");
    expect(container.querySelector("strong")).toHaveTextContent("Bump");
    expect(container.querySelector("code")).toHaveTextContent("api");
    expect(container.querySelector("p")).toBeNull();
  });

  it("BlockText_ParagraphsAndLists", () => {
    const { container } = render(<BlockText text={"First.\n\n- one\n- two"} />);
    expect(container.querySelectorAll("p")).toHaveLength(1);
    expect(container.querySelectorAll("li")).toHaveLength(2);
  });

  it("decisionParts_SplitsTheBoldLead", () => {
    expect(decisionParts("**Outbox**: at-least-once delivery", null)).toEqual({ title: "Outbox", reason: "at-least-once delivery" });
    expect(decisionParts("Use the outbox", "at-least-once")).toEqual({ title: "Use the outbox", reason: "at-least-once" });
    expect(decisionParts("Use the outbox", null)).toEqual({ title: "Use the outbox", reason: null });
  });

  it("DecisionLine_BoldTitleDimReason_ClickOpensTheWholeReason", () => {
    const { container } = render(<DecisionLine name={"**Lockfile regeneration**: used `npm install --package-lock-only` so the lockfile matches."} reason={null} />);
    expect(container.querySelector("strong")).toHaveTextContent("Lockfile regeneration");
    expect(screen.getByTestId("decision-line").textContent).not.toContain("**");
    expect(screen.getByTestId("decision-reason")).toHaveTextContent("used npm install --package-lock-only so the lockfile matches.");
    fireEvent.click(screen.getByRole("button"));
    expect(screen.getByTestId("decision-full")).toHaveTextContent("used npm install --package-lock-only so the lockfile matches.");
    expect(screen.queryByTestId("decision-reason")).toBeNull();
  });

  // 2026-11-22-7103a: a GFM table renders as a table with header cells, inside a wrapper that scrolls sideways.
  it("BlockText_Table_RendersHeaderCellsInsideAScrollWrapper", () => {
    const { container } = render(<BlockText text={"The spec is done.\n| Check | Result |\n|---|---|\n| build | green |"} />);
    const wrapper = container.querySelector(".rl-md-table");
    expect(wrapper?.querySelector("table")).not.toBeNull();
    expect([...container.querySelectorAll("th")].map((th) => th.textContent)).toEqual(["Check", "Result"]);
    expect(container.querySelector("td")).toHaveTextContent("build");
  });

  it("MasterReport_Default_ClosedUnderMastersReport_OpensOnClick", () => {
    render(<MasterReport text={"## Delivered\n\n- the retry"} testId="report" />);
    const report = screen.getByTestId("report");
    expect(report.tagName).toBe("DETAILS");
    expect(report).not.toHaveAttribute("open");
    expect(report.querySelector("summary")).toHaveTextContent(/^Master's report$/);
    fireEvent.click(report.querySelector("summary")!);
    expect(report).toHaveAttribute("open");
    expect(report.querySelector("li")).toHaveTextContent("the retry");
  });

  it("MasterReport_SeveralSpecs_LabelsEachWithItsSpec", () => {
    render(<MasterReport text="Done." spec="Spec 2 · b2" testId="report" />);
    expect(screen.getByTestId("report").querySelector("summary")).toHaveTextContent(/^Spec 2 · b2 · Master's report$/);
  });

  it("MasterReport_Unfolded_IsShownWholeInTheReportFace", () => {
    render(<MasterReport text="Done." testId="report" folded={false} />);
    const report = screen.getByTestId("report");
    expect(report.tagName).toBe("DIV");
    expect(report).toHaveClass("rl-report");
    expect(report).toHaveTextContent(/^Done\.$/);
  });
});
