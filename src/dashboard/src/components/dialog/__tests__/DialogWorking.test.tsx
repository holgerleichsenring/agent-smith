import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import type { SpecDialogActivityPush, SpecDialogReadingPush } from "@/types/spec-dialog";
import { DialogWorking } from "../DialogWorking";
import { earlierStepsLabel, mergedSteps } from "../turnSteps";

const TURN = "2026-11-09T10:00:00Z";

function step(seq: number): SpecDialogActivityPush {
  return {
    dialogId: "d",
    turnStartedAt: TURN,
    seq,
    at: TURN,
    kind: "tool",
    tool: `read_${seq}`,
    summary: null,
    model: null,
    intent: null,
  } as unknown as SpecDialogActivityPush;
}

function reading(state: SpecDialogReadingPush["state"]): SpecDialogReadingPush {
  return { dialogId: "d", kind: "repository", name: "repo-a", state, at: TURN };
}

function steps(from: number, to: number): SpecDialogActivityPush[] {
  return Array.from({ length: to - from + 1 }, (_, i) => step(from + i));
}

describe("DialogWorking (2026-11-09-9631c)", () => {
  it("names a repository whose sandbox is reopening, under the opening header", () => {
    render(<DialogWorking readings={[reading("reopening")]} activity={[]} since={null} />);

    expect(screen.getByTestId("dialog-reading")).toHaveTextContent("repo-a: removed, reopening");
    expect(screen.getByTestId("dialog-working")).toHaveTextContent("Opening the repositories it needs…");
  });

  it("keeps a long turn whole: the row follows the count and lists what it says", () => {
    const activity = mergedSteps([], steps(1, 438));

    render(<DialogWorking readings={[]} activity={activity} since={null} />);

    const row = screen.getByTestId("dialog-activity-folded");
    expect(row).toHaveTextContent("435 earlier steps");
    expect(screen.getByTestId("dialog-working-pulse")).toHaveTextContent("438 steps");
    expect(within(row).getByTestId("dialog-activity-earlier").children).toHaveLength(435);
  });

  it("says how many it lists when the page holds fewer than the turn took", () => {
    render(<DialogWorking readings={[]} activity={steps(401, 438)} since={null} />);

    expect(screen.getByTestId("dialog-activity-folded")).toHaveTextContent(
      "435 earlier steps, the last 35 listed",
    );
  });

  it("words one step in the singular and adds nothing when all are listed", () => {
    expect(earlierStepsLabel(1, 1)).toBe("1 earlier step");
    expect(earlierStepsLabel(47, 47)).toBe("47 earlier steps");
  });
});
