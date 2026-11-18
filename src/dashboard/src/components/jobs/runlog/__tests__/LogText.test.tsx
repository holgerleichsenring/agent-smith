import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { decisionParts } from "../decisionParts";
import { BlockText, InlineText } from "../LogText";
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
});
