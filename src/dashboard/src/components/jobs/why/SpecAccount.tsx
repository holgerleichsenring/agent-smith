"use client";

import type { RunCallPoint, RunCommandPoint, RunSpecStatistics } from "@/lib/runStoryApi";
import { formatChars, formatMs } from "./callSeries";
import { CallSizePlot } from "./CallSizePlot";
import { CallTable } from "./CallTable";
import { CommandTable } from "./CommandTable";
import { useDateLocale, useT } from "@/hooks/useT";

// p0423b: one spec, accounted for. What it cost, what it ran and how each command ended,
// and the shape of every model call inside it. A spec that failed names the command that
// returned non-zero; a spec that stalled shows the prompt climbing while the answer fell.

export interface SpecAccountProps {
  /** 2026-11-09-0620a: the run the calls belong to, so a call's prompt can be opened. */
  runId?: string;
  spec: RunSpecStatistics;
  calls: RunCallPoint[];
  commands: RunCommandPoint[];
}

export function SpecAccount({ runId, spec, calls, commands }: SpecAccountProps) {
  const t = useT();
  const failed = spec.failedCommands > 0 || spec.calls.failedCalls > 0;
  return (
    <section className="card" data-testid="spec-account" data-spec={spec.specId ?? ""}>
      <div className="card-h">
        <h3>{spec.specId ? t("runs.why.spec.title", { spec: spec.specId }) : t("runs.why.spec.outside")}</h3>
        <span className={failed ? "badge bad" : "badge neu"} data-testid="spec-verdict">
          {failed ? t("runs.why.spec.didNotPass") : t("runs.why.spec.nothingFailed")}
        </span>
      </div>
      <div className="card-b">
        <SpecNumbers spec={spec} />
        <h4 style={HEADING}>{t("runs.why.spec.callsHeading")}</h4>
        <CallSizePlot calls={calls} />
        <h4 style={HEADING}>{t("runs.why.spec.commandsHeading")}</h4>
        <CommandTable commands={commands} />
        <h4 style={HEADING}>{t("runs.why.spec.callsInFull")}</h4>
        <CallTable runId={runId} calls={calls} />
      </div>
    </section>
  );
}

const HEADING: React.CSSProperties = {
  fontSize: "11px",
  letterSpacing: "0.1em",
  textTransform: "uppercase",
  color: "var(--ink-3)",
  fontWeight: 600,
  margin: "18px 0 6px",
};

function SpecNumbers({ spec }: { spec: RunSpecStatistics }) {
  const t = useT();
  const locale = useDateLocale();
  const stats = spec.calls;
  return (
    <div className="health health-plain" data-testid="spec-numbers">
      <Metric label={t("runs.why.spec.steps")} value={String(spec.steps)} />
      <Metric label={t("runs.why.spec.took")} value={formatMs(spec.durationMs)} />
      <Metric
        label={t("runs.why.cost.calls")}
        value={String(stats.calls)}
        note={stats.failedCalls > 0 ? t("runs.why.cost.didNotEndWell", { count: stats.failedCalls }) : undefined}
      />
      <Metric label={t("runs.why.cost.largestPrompt")} value={formatChars(stats.largestPromptChars, locale)} />
      <Metric label={t("runs.why.spec.smallestAnswer")} value={formatChars(stats.smallestResponseChars, locale)} />
      <Metric
        label={t("runs.why.spec.commands")}
        value={String(spec.commands)}
        note={spec.failedCommands > 0 ? t("runs.why.spec.nonZero", { count: spec.failedCommands }) : undefined}
      />
      <Metric
        label={t("runs.why.spec.toolOutput")}
        value={formatChars(stats.toolOutputChars, locale)}
        note={
          stats.toolCharsNeverDelivered > 0
            ? t("runs.why.spec.neverReached", { chars: formatChars(stats.toolCharsNeverDelivered, locale) })
            : undefined
        }
      />
      <Metric label={t("runs.why.cost.retries")} value={String(stats.retries)} />
    </div>
  );
}

function Metric({ label, value, note }: { label: string; value: string; note?: string }) {
  return (
    <div className="metric">
      <span className="k">{label}</span>
      <span className="v num">
        {value}
        {note && <small> {note}</small>}
      </span>
    </div>
  );
}
