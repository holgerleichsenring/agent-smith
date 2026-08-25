import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { VerifySummary } from "../VerifySummary";
import type { RunAcceptance } from "@/types/hub-events";
import type { VerifyFallbackView } from "../verifyFallback";

const EXPECTATION = {
  observed: "Login button does nothing",
  expected: ["Clicking login authenticates the user", "A failed login shows an error"],
  constraints: ["No new dependencies"],
};

function fallback(overrides: Partial<VerifyFallbackView>): VerifyFallbackView {
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

const ACCEPTANCE: RunAcceptance = {
  criteria: [
    { text: "Login authenticates the user", status: "met", reason: null },
    { text: "Failed login shows an error", status: "unmet", reason: "no test exercised the failure path" },
    { text: "Works on IE11", status: "not_applicable", reason: "browser no longer supported" },
    { text: "P95 latency under 200ms", status: "unproven", reason: "no benchmark in the diff" },
  ],
  outcome: "edited",
  ratifiedBy: "holger",
};

describe("VerifySummary (the operator's judgement)", () => {
  // 2026-08-25-e257: a run that fails on a wrong criterion is indistinguishable from one
  // that fails on a right one. This is where the difference gets written down — a label,
  // never a control.
  const ACCOUNTED = {
    ...ACCEPTANCE,
    source: "delivery_account" as const,
    criteria: [{ text: "Explicit publish routes", status: "unmet" as const, reason: "no file" }],
  };

  const JUDGED = [
    {
      criterion: "Explicit publish routes",
      machineStatus: "unmet" as const,
      humanStatus: "met" as const,
      reason: "the extension declares six",
      author: "holger",
      recordedAt: "2026-08-25T00:00:00Z",
    },
  ];

  it("VerifySummary_NoJudgementHandlers_OffersNoControl", () => {
    render(<VerifySummary acceptance={ACCOUNTED} fallback={fallback({})} />);
    expect(screen.queryByTestId("criterion-judgement-open")).toBeNull();
  });

  it("VerifySummary_ACriterionWithNoJudgement_OffersToRecordOne", () => {
    render(
      <VerifySummary
        acceptance={ACCOUNTED}
        fallback={fallback({})}
        judgements={[]}
        onRecordJudgement={() => {}}
        onWithdrawJudgement={() => {}}
      />,
    );
    expect(screen.getByTestId("criterion-judgement-open")).toHaveTextContent(
      "this verdict is wrong",
    );
  });

  it("VerifySummary_AnOverruledCriterion_ShowsBothTheMachineAndTheHumanVerdict", () => {
    render(
      <VerifySummary
        acceptance={ACCOUNTED}
        fallback={fallback({})}
        judgements={JUDGED}
        onRecordJudgement={() => {}}
        onWithdrawJudgement={() => {}}
      />,
    );
    expect(screen.getByTestId("verify-criterion")).toHaveAttribute("data-status", "unmet");
    const judgement = screen.getByTestId("criterion-judgement");
    expect(judgement).toHaveTextContent("holger says: met");
    expect(judgement).toHaveTextContent("the extension declares six");
  });

  it("VerifySummary_AnOverrule_CanBeWithdrawn", () => {
    const withdrawn: string[] = [];
    render(
      <VerifySummary
        acceptance={ACCOUNTED}
        fallback={fallback({})}
        judgements={JUDGED}
        onRecordJudgement={() => {}}
        onWithdrawJudgement={(criterion) => withdrawn.push(criterion)}
      />,
    );
    screen.getByTestId("criterion-judgement-withdraw").click();
    expect(withdrawn).toEqual(["Explicit publish routes"]);
  });
});

describe("VerifySummary (the judge that decided)", () => {
  // 2026-08-25-7f5a: an independent read of the branch and the agent's report of its own
  // work are not the same evidence, and a page that shows them identically invites the
  // second to be read as the first.
  it("VerifySummary_ADeliveryAccount_SaysTheGateDecidedOnIt", () => {
    render(
      <VerifySummary
        acceptance={{ ...ACCEPTANCE, source: "delivery_account" }}
        fallback={fallback({})}
      />,
    );
    expect(screen.getByTestId("verify-source")).toHaveTextContent("what the gate decided on");
    expect(screen.getByTestId("verify-source")).toHaveTextContent("no account of the work");
  });

  it("VerifySummary_AMasterVerification_SaysItIsTheAgentsOwnReport", () => {
    render(
      <VerifySummary
        acceptance={{ ...ACCEPTANCE, source: "master_verification" }}
        fallback={fallback({})}
      />,
    );
    expect(screen.getByTestId("verify-source")).toHaveTextContent("reported on its own");
    expect(screen.getByTestId("verify-source")).toHaveTextContent("not the independent read");
  });

  it("VerifySummary_ACitedCriterion_ShowsWhatItWasDecidedOn", () => {
    render(
      <VerifySummary
        acceptance={{
          ...ACCEPTANCE,
          source: "delivery_account",
          criteria: [
            {
              text: "Every host declares explicit publish routes",
              status: "met",
              reason: "the extension declares six",
              citation: "src/Messaging/Installer.cs",
            },
          ],
        }}
        fallback={fallback({})}
      />,
    );
    expect(screen.getByTestId("verify-criterion-citation")).toHaveTextContent(
      "src/Messaging/Installer.cs",
    );
  });

  it("VerifySummary_ACriterionWithoutACitation_ShowsNoCitationRow", () => {
    render(<VerifySummary acceptance={ACCEPTANCE} fallback={fallback({})} />);
    expect(screen.queryByTestId("verify-criterion-citation")).toBeNull();
  });
});

describe("VerifySummary (persisted acceptance)", () => {
  it("VerifySummary_Acceptance_RendersPerCriterionDispositions", () => {
    render(<VerifySummary acceptance={ACCEPTANCE} fallback={fallback({})} />);
    expect(screen.getByTestId("verify-summary")).toHaveAttribute("data-source", "acceptance");
    const criteria = screen.getAllByTestId("verify-criterion");
    expect(criteria).toHaveLength(4);
    expect(criteria[0]).toHaveAttribute("data-status", "met");
    expect(criteria[1]).toHaveAttribute("data-status", "unmet");
    expect(criteria[2]).toHaveAttribute("data-status", "not_applicable");
    expect(criteria[3]).toHaveAttribute("data-status", "unproven");
  });

  it("VerifySummary_Acceptance_DispositionPalette_MockCritClasses", () => {
    // p0343c: dispositions map onto the mock's .crit classes — met=pass,
    // unmet=fail, not_applicable/unproven=wait (dashed neutral mark).
    render(<VerifySummary acceptance={ACCEPTANCE} fallback={fallback({})} />);
    const criteria = screen.getAllByTestId("verify-criterion");
    expect(criteria[0].className).toContain("pass");
    expect(criteria[1].className).toContain("fail");
    expect(criteria[2].className).toContain("wait");
    expect(criteria[3].className).toContain("wait");
    expect(criteria[3].querySelector(".c-stat")).toHaveTextContent("unproven");
  });

  it("VerifySummary_Acceptance_RendersReasonText", () => {
    render(<VerifySummary acceptance={ACCEPTANCE} fallback={fallback({})} />);
    const reasons = screen.getAllByTestId("verify-criterion-reason");
    expect(reasons.map((r) => r.textContent)).toEqual([
      "no test exercised the failure path",
      "browser no longer supported",
      "no benchmark in the diff",
    ]);
  });

  it("VerifySummary_Acceptance_OutcomeBadgeAndRatifiedBy", () => {
    render(<VerifySummary acceptance={ACCEPTANCE} fallback={fallback({})} />);
    // p0343c: the badge carries the mock's proven count; one unmet → bad tone.
    expect(screen.getByTestId("verify-outcome-badge")).toHaveTextContent("1 of 4 · 1 failed");
    expect(screen.getByTestId("verify-ratified-by")).toHaveTextContent("ratified by holger");
  });

  it("VerifySummary_Acceptance_NoCriteria_HonestEmptyState", () => {
    render(
      <VerifySummary
        acceptance={{ criteria: [], outcome: null, ratifiedBy: null }}
        fallback={fallback({})}
      />,
    );
    expect(screen.getByTestId("verify-empty")).toHaveTextContent("nothing has been proven green");
    const badge = screen.getByTestId("verify-outcome-badge");
    expect(badge).toHaveTextContent("No ratified contract");
    expect(badge.className).not.toContain("ok");
  });

  it("VerifySummary_Acceptance_RejectedOutcome_BadBadgeNeverGreen", () => {
    render(
      <VerifySummary
        acceptance={{ ...ACCEPTANCE, outcome: "rejected" }}
        fallback={fallback({})}
      />,
    );
    const badge = screen.getByTestId("verify-outcome-badge");
    expect(badge.className).toContain("bad");
    expect(badge.className).not.toContain("ok");
  });
});

describe("VerifySummary (event fallback, pre-p0344b runs)", () => {
  it("VerifySummary_NoAcceptance_UsesEventFallbackView", () => {
    render(<VerifySummary acceptance={null} fallback={fallback({})} />);
    expect(screen.getByTestId("verify-summary")).toHaveAttribute("data-source", "event-fallback");
    expect(screen.getAllByTestId("verify-criterion")).toHaveLength(2);
    expect(screen.getByTestId("verify-outcome-badge")).toHaveTextContent("Ratified verbatim");
  });

  it("VerifySummary_Fallback_Edited_ShowsEditDistance", () => {
    render(
      <VerifySummary acceptance={null} fallback={fallback({ outcome: "edited", editDistance: 7 })} />,
    );
    expect(screen.getByTestId("verify-outcome-badge")).toHaveTextContent("Δ7");
  });

  it("VerifySummary_Fallback_Rejected_RendersRoseBadge_NotGreen", () => {
    render(
      <VerifySummary
        acceptance={null}
        fallback={fallback({ outcome: "rejected", tone: "rose", ratified: false })}
      />,
    );
    const badge = screen.getByTestId("verify-outcome-badge");
    expect(badge.className).toContain("bad");
    expect(badge.className).not.toContain("ok");
  });

  it("VerifySummary_Fallback_NoRatification_ShowsHonestEmptyState_NeverGreen", () => {
    render(
      <VerifySummary
        acceptance={null}
        fallback={fallback({ outcome: "none", tone: "neutral", ratified: false, expectation: null })}
      />,
    );
    expect(screen.getByTestId("verify-empty")).toHaveTextContent("nothing has been proven green");
    const badge = screen.getByTestId("verify-outcome-badge");
    expect(badge.className).not.toContain("ok");
  });

  it("VerifySummary_Fallback_Unratified_CriteriaNotShownAsProven", () => {
    render(
      <VerifySummary
        acceptance={null}
        fallback={fallback({ outcome: "unratified", tone: "neutral", ratified: false })}
      />,
    );
    const badge = screen.getByTestId("verify-outcome-badge");
    expect(badge.className).not.toContain("ok");
  });
});
