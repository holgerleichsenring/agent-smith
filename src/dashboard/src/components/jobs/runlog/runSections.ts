import type { RunStepRow } from "@/lib/runStepsApi";
import { splitSpecPrefix } from "@/lib/runStepRail";
import { EventType, type RunAcceptance, type RunEvent, type StepStartedEvent, type VerifyPassRecordedEvent } from "@/types/hub-events";
import type { StorySectionId } from "./storySections";

// 2026-11-16-6877b: a run read as seven sections of text instead of a graph. Every step row the server projects is
// placed in one section by its command name; nothing here asks the server for new data. A build or verify step that
// follows a review in the same spec is the fix pass, so it reads under "Reviewed and fixed". A command this table does
// not name joins the section of the step before it (the first one joins "Got ready"), so a new command is never lost.
// "Checked against the spec" holds the run's acceptance verdicts, not steps.

export type StepSectionId = "ready" | "spec" | "build" | "verify" | "review" | "check" | "deliver";
/** 2026-11-17-d6fbb: the seven step sections and the three story sections (storySections.ts). */
export type SectionId = StepSectionId | StorySectionId;

export const SECTIONS: ReadonlyArray<{ id: StepSectionId; title: string; short: string }> = [
  { id: "ready", title: "Got ready", short: "Ready" },
  { id: "spec", title: "Derived the spec", short: "Spec" },
  { id: "build", title: "Built it", short: "Build" },
  { id: "verify", title: "Verified the build", short: "Verify" },
  { id: "review", title: "Reviewed and fixed", short: "Review" },
  { id: "check", title: "Checked against the spec", short: "Check" },
  { id: "deliver", title: "Delivered", short: "Deliver" },
];

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
  SkillRound: "build", FilterRound: "build", SwitchSkill: "build", RunFinalPhase: "build", GenerateTests: "build",
  GenerateDocs: "build", CompileDiscussion: "build", CommitSpecWork: "build",
  VerifySpec: "verify", RunVerifyPhase: "verify", ConvergenceCheck: "verify",
  ReviewSpecDiff: "review", RunReviewPhase: "review",
  WriteSpecRecord: "deliver", WriteRunResult: "deliver", CommitAndPR: "deliver", PrCrossLink: "deliver",
  PersistWorkBranch: "deliver", DeliverOutput: "deliver", InitCommit: "deliver", InitComplete: "deliver", CompletePullRequests: "deliver", RefreshBranchBase: "deliver", DeployToEnvironment: "deliver",
  PostPrComments: "deliver", WriteTickets: "deliver", DeliverFindings: "deliver", FileTickets: "deliver",
};

/** "AgenticMasterCommand:portal-api" and "AgenticMaster" both name AgenticMaster. */
export function commandKey(row: RunStepRow): string {
  const name = row.commandName ?? row.stepName;
  const base = name.includes(":") ? name.slice(0, name.indexOf(":")) : name;
  return base.endsWith("Command") ? base.slice(0, -"Command".length) : base;
}

/** done = green, warn = a finding or running (amber), bad = a step failed (red), pending = hollow. */
export type SectionState = "done" | "warn" | "running" | "bad" | "pending";

export interface LogLine {
  stepIndex: number;
  /** "06:05:48" from the step's StepStarted event, when the page holds it. */
  at: string | null;
  text: string;
  detail: string | null;
  state: SectionState;
  /** 2026-11-17-d6fbc: a verify step's pass as data, when the page holds its event; its prose detail is then not drawn. */
  verify?: VerifyPassRecordedEvent | null;
}

export interface RunSection {
  id: SectionId;
  title: string;
  short: string;
  state: SectionState;
  summary: string;
  seconds: number;
  lines: LogLine[];
}

export function runSections(steps: RunStepRow[], events: RunEvent[], checkState: SectionState, checkSummary: string): RunSection[] {
  const started = new Map<number, string>();
  const passes = new Map<number, VerifyPassRecordedEvent>();
  for (const e of events) {
    if (e.type === EventType.StepStarted) started.set((e as StepStartedEvent).stepIndex, e.timestamp);
    else if (e.type === EventType.VerifyPassRecorded && e.originStepIndex != null) passes.set(e.originStepIndex, e as VerifyPassRecordedEvent);
  }
  const placed = placeSteps(steps);
  return SECTIONS.map(({ id, title, short }) => {
    if (id === "check") return { id, title, short, state: checkState, summary: checkSummary, seconds: 0, lines: [] };
    const rows = placed.filter((p) => p.section === id).map((p) => p.row);
    const lines = rows.map((row) => line(row, started.get(row.stepIndex) ?? null, passes.get(row.stepIndex) ?? null));
    return { id, title, short, state: sectionState(rows), summary: sectionSummary(rows), seconds: rows.reduce((n, r) => n + (r.durationSeconds ?? 0), 0), lines };
  });
}

function placeSteps(steps: RunStepRow[]): Array<{ row: RunStepRow; section: StepSectionId }> {
  const out: Array<{ row: RunStepRow; section: StepSectionId }> = [];
  let previous: StepSectionId = "ready";
  let reviewedSpec: string | null | undefined = undefined;
  for (const row of [...steps].sort((l, r) => l.stepIndex - r.stepIndex)) {
    const key = commandKey(row);
    const spec = row.specId ?? splitSpecPrefix(row.displayName ?? row.stepName).specId;
    let section: StepSectionId = SECTION_OF[key] ?? previous;
    if (section === "spec" && key === "SelectSpec") reviewedSpec = undefined;
    if (section === "review") reviewedSpec = spec;
    else if ((section === "build" || section === "verify") && reviewedSpec !== undefined && reviewedSpec === spec) section = "review";
    out.push({ row, section });
    previous = section;
  }
  return out;
}

function line(row: RunStepRow, startedAt: string | null, verify: VerifyPassRecordedEvent | null): LogLine {
  return {
    stepIndex: row.stepIndex,
    at: startedAt ? clockTime(startedAt) : null,
    text: splitSpecPrefix(row.displayName ?? row.stepName).label,
    detail: row.resultMessage,
    state: row.planned ? "pending" : stepState(row),
    verify,
  };
}

function stepState(row: RunStepRow): SectionState {
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

/** The newest result message the section's steps left, else how many steps ran. */
function sectionSummary(rows: RunStepRow[]): string {
  const reached = rows.filter((r) => !r.planned && r.status);
  if (reached.length === 0) return rows.length > 0 ? "not reached yet" : "nothing ran here";
  const said = [...reached].reverse().find((r) => r.resultMessage)?.resultMessage;
  if (said) return said.split(/\r?\n/)[0];
  return reached.length === 1 ? "1 step" : `${reached.length} steps`;
}

function clockTime(iso: string): string | null {
  const at = new Date(iso);
  return Number.isNaN(at.getTime()) ? null : at.toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit", second: "2-digit", hour12: false });
}

/** The check section from the run's acceptance verdicts: red when one is unmet, amber when one is unproven. */
export function checkOf(acceptance: RunAcceptance | null | undefined): { state: SectionState; summary: string } {
  const criteria = acceptance?.criteria ?? [];
  if (criteria.length === 0) return { state: "pending", summary: "no verdict recorded" };
  const met = criteria.filter((c) => c.status === "met").length;
  const words = `${met} of ${criteria.length} done criteria met`;
  if (criteria.some((c) => c.status === "unmet")) return { state: "bad", summary: words };
  if (criteria.some((c) => c.status === "unproven")) return { state: "warn", summary: words };
  return { state: "done", summary: words };
}

/** "28 s", "1 min 07 s", "1 h 05 min". */
export function sectionDuration(seconds: number): string {
  const s = Math.round(seconds);
  if (s <= 0) return "";
  if (s < 60) return `${s} s`;
  if (s < 3600) return `${Math.floor(s / 60)} min ${String(s % 60).padStart(2, "0")} s`;
  return `${Math.floor(s / 3600)} h ${String(Math.floor((s % 3600) / 60)).padStart(2, "0")} min`;
}
