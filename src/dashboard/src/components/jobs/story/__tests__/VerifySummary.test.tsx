import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { VerifySummary } from "../VerifySummary";
import type { VerifyView } from "../beatMapping";

const EXPECTATION = {
  observed: "Login button does nothing",
  expected: ["Clicking login authenticates the user", "A failed login shows an error"],
  constraints: ["No new dependencies"],
};

function view(overrides: Partial<VerifyView>): VerifyView {
  return {
    outcome: "verbatim",
    ratifiedBy: "holger",
    editDistance: 0,
    expectation: EXPECTATION,
    tone: "green",
    ratified: true,
    ...overrides,
  };
}

describe("VerifySummary", () => {
  it("VerifySummary_Ratified_RendersCriteria", () => {
    render(<VerifySummary view={view({})} />);
    const criteria = screen.getAllByTestId("verify-criterion");
    expect(criteria).toHaveLength(2);
    expect(screen.getByTestId("verify-outcome-badge")).toHaveTextContent("Ratified verbatim");
  });

  it("VerifySummary_Edited_ShowsEditDistance", () => {
    render(<VerifySummary view={view({ outcome: "edited", editDistance: 7 })} />);
    expect(screen.getByTestId("verify-outcome-badge")).toHaveTextContent("Δ7");
  });

  it("VerifySummary_Rejected_RendersRoseBadge_NotGreen", () => {
    render(<VerifySummary view={view({ outcome: "rejected", tone: "rose", ratified: false })} />);
    const badge = screen.getByTestId("verify-outcome-badge");
    expect(badge.className).toContain("rose");
    expect(badge.className).not.toContain("emerald");
  });

  it("VerifySummary_NoRatification_ShowsHonestEmptyState_NeverGreen", () => {
    render(
      <VerifySummary
        view={view({ outcome: "none", tone: "neutral", ratified: false, expectation: null })}
      />,
    );
    expect(screen.getByTestId("verify-empty")).toHaveTextContent("nothing has been proven green");
    const badge = screen.getByTestId("verify-outcome-badge");
    expect(badge.className).not.toContain("emerald");
  });

  it("VerifySummary_Unratified_CriteriaNotShownAsProven", () => {
    render(
      <VerifySummary
        view={view({ outcome: "unratified", tone: "neutral", ratified: false })}
      />,
    );
    // Criteria still listed but the outcome badge is neutral, never green.
    const badge = screen.getByTestId("verify-outcome-badge");
    expect(badge.className).not.toContain("emerald");
  });
});
