import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { CriteriaMetCard } from "@/components/overview/CriteriaMetCard";
import type { CriterionCounts } from "@/lib/expectationsApi";

// The Criteria met card: the share of judged criteria met, with its counts beneath it,
// and a dash — never 0% — where nothing was judged.

const counts = (over: Partial<CriterionCounts>): CriterionCounts => ({
  met: 0,
  unmet: 0,
  unproven: 0,
  notApplicable: 0,
  overruled: 0,
  staleOverrules: 0,
  judged: 0,
  share: null,
  ...over,
});

describe("CriteriaMetCard", () => {
  it("CriteriaMetCard_RendersShareAndCounts", () => {
    const judged = counts({
      met: 3,
      unmet: 1,
      unproven: 1,
      notApplicable: 2,
      judged: 5,
      share: 0.6,
    });
    render(
      <CriteriaMetCard read={{ data: { runs: 2, counts: judged, projects: [] }, error: null }} />,
    );

    const card = screen.getByTestId("overview-criteria-card");
    expect(card.querySelector(".v")).toHaveTextContent("60%");
    expect(card).toHaveTextContent("3 met of 5 judged · 2 not applicable · 2 coding runs");
    expect(card.querySelector(".meter")).not.toBeNull();
  });

  it("CriteriaMetCard_NothingJudged_ShowsADashNotZero", () => {
    const none = counts({ notApplicable: 4 });
    render(
      <CriteriaMetCard read={{ data: { runs: 1, counts: none, projects: [] }, error: null }} />,
    );

    const card = screen.getByTestId("overview-criteria-card");
    expect(card.querySelector(".v")).toHaveTextContent("—");
    expect(card).toHaveTextContent("1 coding run · no criterion judged yet");
    expect(card.querySelector(".meter")).toBeNull();
  });

  it("CriteriaMetCard_ReadFailed_SaysSo", () => {
    render(<CriteriaMetCard read={{ data: null, error: new Error("down") }} />);

    expect(screen.getByTestId("overview-criteria-card")).toHaveTextContent("Criteria unavailable");
  });
});
