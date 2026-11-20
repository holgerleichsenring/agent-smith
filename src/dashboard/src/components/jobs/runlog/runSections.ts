import type { RunStepRow } from "@/lib/runStepsApi";
import type { RunAcceptance } from "@/types/hub-events";

// 2026-11-16-6877b: which section of the run a step's command belongs to, and the states a step and a section read in.
// 2026-11-18-d6d2e: the seven fixed sections this file drew for the ticket page are gone — both pages draw
// runLogSections; what stays is the command table it places steps by, the step and section states, and the check.

export type StepSectionId = "ready" | "spec" | "build" | "verify" | "review" | "check" | "deliver";

const SECTION_OF: Record<string, StepSectionId> = {
  LoadCatalog: "ready", PipelineNameInitializer: "ready", FetchTicket: "ready", ScopeRepos: "ready",
  CheckoutSource: "ready", TryCheckoutSource: "ready", RunPreflight: "ready", SetupRegistryAuth: "ready",
  EnsurePrerequisites: "ready", ProbeTarget: "ready", MaterializeReferenceSets: "ready", BootstrapProject: "ready",
  BootstrapCheck: "ready", BootstrapGate: "ready", LoadCodeMap: "ready", LoadCachedCodeMap: "ready",
  LoadCodingPrinciples: "ready", LoadMemoryIndex: "ready", LoadDesignSystem: "ready", LoadContext: "ready",
  LoadSkills: "ready", LoadRuns: "ready", LoadSwagger: "ready", AcquireSource: "ready", BootstrapDocument: "ready",
  SessionSetup: "ready", PublishProjectLanguage: "ready", GroundSpecDialog: "ready",
  RunConfigPreflight: "ready", PlanFiling: "ready", // 2026-11-17-b6e4b
  Triage: "spec", DeriveSpec: "spec", SpecHandback: "spec", ReviewSpecs: "spec", SpecSequence: "spec",
  SelectSpec: "spec", EmptyPlanCheck: "spec", Approval: "spec", CheckSpecPremises: "spec", SpecGate: "spec",
  Ask: "spec", GeneratePlan: "spec", PlanOpenQuestions: "spec", RatifyScanContract: "spec",
  AnalyzeCode: "build", AgenticExecute: "build", AgenticMaster: "build", MasterOpenQuestions: "build",
  AdminAssistantTurn: "build", // 2026-11-17-f3a8a
  SkillRound: "build", FilterRound: "build", SwitchSkill: "build", RunFinalPhase: "build", GenerateTests: "build",
  GenerateDocs: "build", CompileDiscussion: "build", CommitSpecWork: "build",
  VerifySpec: "verify", RunVerifyPhase: "verify", ConvergenceCheck: "verify",
  ReviewSpecDiff: "review", RunReviewPhase: "review",
  WriteSpecRecord: "deliver", WriteRunResult: "deliver", CommitAndPR: "deliver", PrCrossLink: "deliver",
  PersistWorkBranch: "deliver", DeliverOutput: "deliver", InitCommit: "deliver", InitComplete: "deliver", CompletePullRequests: "deliver", RefreshBranchBase: "deliver", DeployToEnvironment: "deliver",
  PostPrComments: "deliver", WriteTickets: "deliver", DeliverFindings: "deliver", FileTickets: "deliver",
};

/** 2026-11-17-6d9eb: the step section a command belongs to, or undefined for a command the table does not name. */
export function commandSection(row: RunStepRow): StepSectionId | undefined {
  return SECTION_OF[commandKey(row)];
}

/** "AgenticMasterCommand:portal-api" and "AgenticMaster" both name AgenticMaster. */
export function commandKey(row: RunStepRow): string {
  const name = row.commandName ?? row.stepName;
  const base = name.includes(":") ? name.slice(0, name.indexOf(":")) : name;
  return base.endsWith("Command") ? base.slice(0, -"Command".length) : base;
}

/** done = green, warn = a finding or running (amber), bad = a step failed (red), pending = hollow. */
export type SectionState = "done" | "warn" | "running" | "bad" | "pending";

export function stepState(row: RunStepRow): SectionState {
  if (row.status === "failed" || row.status === "error") return "bad";
  if (row.status === "running") return "running";
  if (row.hasFinding) return "warn";
  return row.status ? "done" : "pending";
}

/** A failed step outranks a running one, which outranks a finding; a section of unreached steps is pending. */
export function sectionState(rows: RunStepRow[]): SectionState {
  const reached = rows.filter((r) => !r.planned && r.status);
  if (reached.length === 0) return "pending";
  const states = reached.map(stepState);
  for (const s of ["bad", "running", "warn"] as const) if (states.includes(s)) return s;
  return "done";
}

export interface RunCheck {
  state: SectionState;
  summary: string;
  title: string;
}

/** The check section from the run's acceptance verdicts: red when one is unmet or an account went unmeasured, amber when
 *  one is unproven. 2026-11-19-1b72c: null when the run recorded no verdict, no report and no unmeasured account — the
 *  per-criterion verdict was retired (2026-11-18-98fa), so such a run has no check to show, not a "no verdict" line. */
export function checkOf(acceptance: RunAcceptance | null | undefined): RunCheck | null {
  const check = recordedCheck(acceptance);
  if (!acceptance?.unmeasured?.length) return check;
  return { state: "bad", summary: check ? `${check.summary} · not measured` : "not measured", title: check?.title ?? CHECK_TITLE };
}

function recordedCheck(acceptance: RunAcceptance | null | undefined): RunCheck | null {
  const criteria = acceptance?.criteria ?? [];
  // 2026-11-18-98fa: masters that ended with finish_spec judged no line — the section is their report.
  if (criteria.length === 0 && acceptance?.reports?.length) {
    const n = acceptance.followUps?.length ?? 0;
    return { state: "done", summary: n > 0 ? `reported · ${n} follow-up${n === 1 ? "" : "s"}` : "reported", title: REPORT_TITLE };
  }
  if (criteria.length === 0) return null;
  // A run stored before 98fa keeps its recorded verdicts and their count (98fa: "Runs stored before keep their counts").
  const met = criteria.filter((c) => c.status === "met").length;
  const summary = `${met} of ${criteria.length} done criteria met`;
  const state = criteria.some((c) => c.status === "unmet") ? "bad" : criteria.some((c) => c.status === "unproven") ? "warn" : "done";
  return { state, summary, title: CHECK_TITLE };
}

const CHECK_TITLE = "Checked against the spec";
const REPORT_TITLE = "The master's report";
