import { SERIES_TOKEN } from "@/components/dialog/DialogProposalRounds";
import type { SpecDialogProposalPush } from "@/types/spec-dialog";

// 2026-11-11-8714: the approval card says the review in one line. The rounds and findings themselves sit under each
// spec row of the turn result above, so the card counts them instead of listing them a second time. Nothing to
// count gives no line: an empty line would read as a clearance the review never gave.

type Counted = Pick<SpecDialogProposalPush, "reviewRounds" | "findings" | "notReviewed">;

export function approvalReviewLine(proposal: Counted): string | null {
  const last = new Map<string, string>();
  for (const round of proposal.reviewRounds ?? []) {
    if (round.specId === SERIES_TOKEN) continue;
    last.set(round.specId, normalised(round.verdict));
  }
  const verdicts = [...last.values()];
  const refused = verdicts.filter((v) => v === "refuse").length;
  const cuts = verdicts.filter((v) => v === "build-with-cuts").length;
  const clean = verdicts.filter((v) => v === "build").length;
  const findings = proposal.findings?.length ?? 0;
  const waiting = proposal.notReviewed?.length ?? 0;
  const parts: string[] = [];
  if (verdicts.length > 0) {
    const said = [
      refused > 0 ? `${refused} refused` : null,
      cuts > 0 ? `${cuts} need cuts` : null,
      clean > 0 ? `${clean} clean` : null,
    ].filter((part): part is string => part !== null);
    parts.push(`Review of ${verdicts.length} ${verdicts.length === 1 ? "spec" : "specs"}: ${said.join(", ")}.`);
  }
  if (findings > 0) parts.push(`${findings} ${findings === 1 ? "finding" : "findings"}, each under its spec above.`);
  if (waiting > 0) parts.push(`${waiting} not reviewed yet.`);
  return parts.length === 0 ? null : parts.join(" ");
}

function normalised(verdict: string): string {
  return verdict.trim().toLowerCase().replace(/[\s_]+/g, "-");
}
