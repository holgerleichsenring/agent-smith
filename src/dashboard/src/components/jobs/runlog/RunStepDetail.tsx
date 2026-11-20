"use client";

import Link from "next/link";
import { useMemo, useState } from "react";
import { ResultDocument } from "@/components/jobs/ResultTab";
import { useAnalyzeMarkdown } from "@/hooks/useAnalyzeMarkdown";
import { useRunStepEvents } from "@/hooks/useRunStepEvents";
import { useSandboxEvents } from "@/hooks/useSandboxEvents";
import { splitSpecPrefix } from "@/lib/runStepRail";
import type { ProgressLedgerEntry } from "@/types/hub-events";
import type { LogStep } from "./runLogSections";
import { commandKey } from "./runSections";
import { InlineText } from "./LogText";
import { RunStepHead } from "./RunStepHead";
import { StepCallSequence } from "./StepCallSequence";
import { StepPart } from "./StepPart";
import { StepSpecSteps } from "./StepSpecSteps";
import type { GivenPasses } from "./useSpecStepLines";
import { StepSubAgents } from "./StepSubAgents";
import { repoGreenCount, repoOutcomeWords, stepDetailLines, type DetailFilter, type DetailLine, type RepoOutcome } from "./stepDetailLines";

// 2026-11-17-6d9ec: an open step, in place under its line — what the graph's "Full pipeline — step x/n" panel showed,
// drawn as a terminal: the head line, each repository's outcome (and its output while the step runs), the step's
// summary, filters as text links, and every llm call, tool call, write, decision, finding and observation newest first.
// The events are one page of the step, newest first, followed while the run is live; older pages load on request.
// 2026-11-19-1b72d: below the head line, headed parts — the spec's steps (on the step that runs a spec's master), the
// sub-agents, build and tests per repository, and the activity: the event list with its filters, as before.

const FILTERS: Array<{ key: DetailFilter | "all"; label: string }> = [
  { key: "all", label: "All" }, { key: "find", label: "Findings" }, { key: "obs", label: "Obs" }, { key: "tool", label: "Tools" },
  { key: "llm", label: "LLM" }, { key: "file", label: "Files" }, { key: "dec", label: "Decisions" },
];

const CAP = 12;

interface Props {
  runId: string;
  step: LogStep;
  /** The run is still going: the page follows it, and a repository's output can be watched. */
  live: boolean;
  /** The run's progress ledger and the spec it belongs to (specStepLines.ledgerSpec); absent where no spec is known. */
  ledger?: ProgressLedgerEntry[] | null;
  ledgerSpecId?: string | null;
  /** The run's passes as the page already read them (the ticket page's store); absent, they are read on demand. */
  passes?: GivenPasses;
}

export function RunStepDetail({ runId, step, live, ledger = null, ledgerSpecId = null, passes }: Props) {
  const { row } = step;
  const specId = row.specId ?? splitSpecPrefix(row.displayName ?? row.stepName).specId;
  const runsTheSpec = !row.planned && specId !== null && commandKey(row) === "AgenticMaster";
  const page = useRunStepEvents(runId, row.planned ? null : row.stepIndex, live);
  const view = useMemo(() => stepDetailLines(page.events, !live), [page.events, live]);
  const [filter, setFilter] = useState<DetailFilter | "all">("all");
  const [oldestFirst, setOldestFirst] = useState(false);
  const [find, setFind] = useState("");
  const [all, setAll] = useState(false);
  const shown = useMemo(() => {
    const q = find.trim().toLowerCase();
    const list = view.lines.filter((l) => (filter === "all" || l.filter === filter) && (!q || `${l.kind} ${l.text} ${l.meta}`.toLowerCase().includes(q)));
    return oldestFirst ? [...list].reverse() : list;
  }, [view.lines, filter, find, oldestFirst]);
  const visible = all ? shown : shown.slice(0, CAP);

  return (
    <>
      <RunStepHead step={step} />
      {runsTheSpec && <StepSpecSteps runId={runId} specId={specId} ledger={ledger} ledgerIsThisSpec={ledgerSpecId === specId} passes={passes} />}
      {!row.planned && (
        <StepSubAgents events={page.events} live={live} hasOlder={page.hasOlder} loading={page.loading} loadOlder={page.loadOlder} />
      )}
      {view.repos.length > 0 && (
        <StepPart title={`Build and tests · ${repoGreenCount(view.repos)}`} testId="step-part-checks">
          {view.repos.map((r) => <RepoRow key={r.repo} runId={runId} repo={r} live={live} />)}
        </StepPart>
      )}
      {!row.planned && (
        <StepPart title="Activity" testId="step-part-activity">
          <div className="rl-detail-bar" data-testid="step-detail-bar">
            {FILTERS.map((f) => (
              <button key={f.key} type="button" className="rl-filter" aria-pressed={filter === f.key} onClick={() => setFilter(f.key)}
                data-testid={`step-filter-${f.key}`}>{f.label}</button>
            ))}
            <button type="button" className="rl-filter" onClick={() => setOldestFirst((o) => !o)} data-testid="step-order">
              {oldestFirst ? "oldest first" : "newest first"}
            </button>
            <input className="rl-find" value={find} onChange={(e) => setFind(e.target.value)} placeholder="find in this step" aria-label="Find in this step"
              data-testid="step-find" />
            <span className="rl-detail-sum" data-testid="step-detail-summary">
              {view.actions} {view.actions === 1 ? "action" : "actions"} · {view.writes} {view.writes === 1 ? "write" : "writes"} · {view.llm} llm · ${view.costUsd.toFixed(2)}
            </span>
          </div>
          {filter === "llm" ? (
            // 2026-11-18-e06be: the LLM filter is the step's call sequence — numbered, fed and decided, the prompt on a click.
            <StepCallSequence runId={runId} events={page.events} total={row.llmCalls ?? null} hasOlder={page.hasOlder}
              loading={page.loading} loadOlder={page.loadOlder} runEnded={!live} />
          ) : (
            <>
              {page.hasOlder && (
                <div className="rl-detail-older" data-testid="step-events-older-notice">
                  showing this step&rsquo;s newest events; older ones exist ·{" "}
                  <button type="button" className="rl-filter" disabled={page.loading} onClick={page.loadOlder} data-testid="step-events-load-older">
                    {page.loading ? "loading…" : "load older"}
                  </button>
                </div>
              )}
              {visible.map((l) => <Line key={l.key} line={l} />)}
              {shown.length === 0 && (
                <div className="rl-detail-line rl-dim" data-testid="step-detail-empty">{page.loading ? "reading the step…" : "nothing recorded here"}</div>
              )}
              {shown.length > CAP && (
                <button type="button" className="rl-filter rl-more-lines" onClick={() => setAll((a) => !a)} data-testid="step-show-all">
                  {all ? "show fewer" : `show all ${shown.length} ›`}
                </button>
              )}
            </>
          )}
        </StepPart>
      )}
      {commandKey(row) === "AnalyzeCode" && <AnalyzeDoc runId={runId} />}
    </>
  );
}

function Line({ line }: { line: DetailLine }) {
  return (
    <div className="rl-detail-line" data-testid="step-detail-line" data-kind={line.kind} data-tone={line.tone}>
      <span className="rl-at">{line.at}</span>
      <span className="rl-kind">{line.kind}</span>
      <span className="rl-text">
        {line.href ? (line.href.startsWith("/") ? <Link href={line.href}>{line.text}</Link> : <a href={line.href} target="_blank" rel="noreferrer">{line.text} ↗</a>) : <InlineText text={line.text} />}
      </span>
      <span className="rl-meta-col">{line.meta}</span>
    </div>
  );
}

/** One repository's build and test command: where it ran, what ran, how it ended and how long it took. */
function RepoRow({ runId, repo, live }: { runId: string; repo: RepoOutcome; live: boolean }) {
  const state = repo.exitCode === null ? "run" : repo.exitCode === 0 ? "ok" : repo.exitCode === -1 ? "dim" : "bad";
  return (
    <div className="rl-part-row rl-check" data-state={state} data-testid={`step-repo-${repo.repo}`}>
      <span className="rl-part-dot" aria-hidden="true" />
      <span className="rl-part-key">{repo.repo}</span>
      <span className="rl-part-what">{repo.summary}</span>
      <span className="rl-part-state">{repoOutcomeWords(repo)}</span>
      {live && <div className="rl-part-more"><RepoOutput runId={runId} repo={repo.repo} /></div>}
    </div>
  );
}

/** A repository's output, streamed while the step runs; SandboxOutput is never stored, so it exists only live. */
function RepoOutput({ runId, repo }: { runId: string; repo: string }) {
  const [open, setOpen] = useState(false);
  const feed = useSandboxEvents(runId, repo, open);
  return (
    <details className="rl-doc" onToggle={(e) => setOpen((e.target as HTMLDetailsElement).open)} data-testid={`step-output-${repo}`}>
      <summary>output</summary>
      <div className="rl-output">
        {feed.outputs.length === 0 ? <span className="rl-dim">waiting for output…</span> : feed.outputs.map((o, i) => (
          <div key={`${o.batchSeq}-${i}`} className={o.stream === "stderr" ? "rl-bad" : ""}>{o.line}</div>
        ))}
      </div>
    </details>
  );
}

function AnalyzeDoc({ runId }: { runId: string }) {
  const { content } = useAnalyzeMarkdown(runId);
  if (!content) return null;
  return (
    <details className="rl-doc" data-testid="step-analyze">
      <summary>what the agent understood (analyze.md)</summary>
      <div className="rl-doc-body"><ResultDocument content={content} /></div>
    </details>
  );
}
