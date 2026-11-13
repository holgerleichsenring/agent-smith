import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import type { SpecDialogActivityPush, SpecDialogReadingPush } from "@/types/spec-dialog";
import { DialogWorking } from "../DialogWorking";
import { mergedSteps } from "../turnSteps";

const TURN = "2026-11-09T10:00:00Z";

function step(seq: number): SpecDialogActivityPush {
  return {
    dialogId: "d",
    turnStartedAt: TURN,
    seq,
    at: TURN,
    kind: "tool",
    name: "read_file",
    detail: `src/F${seq}.cs`,
  };
}

function reading(state: SpecDialogReadingPush["state"]): SpecDialogReadingPush {
  return { dialogId: "d", kind: "repository", name: "repo-a", state, at: TURN };
}

function steps(from: number, to: number): SpecDialogActivityPush[] {
  return Array.from({ length: to - from + 1 }, (_, i) => step(from + i));
}

describe("DialogWorking", () => {
  it("names a repository whose sandbox is reopening, under the opening header", () => {
    render(<DialogWorking readings={[reading("reopening")]} activity={[]} since={null} />);

    expect(screen.getByTestId("dialog-reading")).toHaveTextContent("repo-a: removed, reopening");
    expect(screen.getByTestId("dialog-working")).toHaveTextContent("Opening the repositories it needs…");
  });

  // 2026-11-13-3dc8: the condensed lines and a clock in minutes and seconds; no step count, no fold.
  it("Live_CondensedLinesAndClock_NoStepCount", () => {
    const activity = mergedSteps([], [
      ...steps(1, 448),
      { ...step(449), kind: "model", name: "Review › stub-model", detail: '{"verdict":"BUILD"}' },
      { ...step(450), kind: "tool", name: "write_spec", detail: "s.yaml" },
    ] as SpecDialogActivityPush[]);

    render(<DialogWorking readings={[]} activity={activity} since={Date.now() - 648_000} />);

    expect(screen.getByTestId("dialog-working-pulse")).toHaveTextContent(/^10m 48s$/);
    expect(within(screen.getByTestId("dialog-activity")).getAllByTestId("dialog-activity-line")
      .map((line) => line.textContent)).toEqual(["448 reads", "write_spec s.yaml"]);
    expect(screen.queryByTestId("dialog-activity-folded")).toBeNull();
    expect(screen.getByTestId("dialog-working")).not.toHaveTextContent(/steps?\b/);
  });
});
