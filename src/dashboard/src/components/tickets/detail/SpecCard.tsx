"use client";

import { useState } from "react";
import { useLacksPermission } from "@/hooks/useLacksPermission";
import { postDefaultVerdicts } from "@/lib/ticketsApi";
import { useTicketDefaults, useTicketDetail } from "@/lib/tickets/TicketPageStore";
import type { TicketDefault, TicketSeriesCriterion, TicketSeriesSpec } from "@/types/tickets";
import { RUNS_CONTROL } from "./RetryUnchanged";
import { criteriaTally } from "./reportWords";
import { MasterReportText } from "./TicketFollowUps";
import { SPEC_PARAM, useTicketTabGo } from "./useTicketTabGo";

// 2026-11-16-6877a: the spec an attempt delivered, as a card inside its report. Closed it names the spec and how many
// done criteria were met (a spec stored with verdicts) or that its master reported — nothing when neither (1b72c);
// open it lists every criterion with its verdict and what the run cited, and the choices the run took without asking
// ("Decided for you"), each kept or changed with the Defaults tab's own verdicts.

export function SpecCard({ spec, runId }: { spec: TicketSeriesSpec; runId: string }) {
  const [open, setOpen] = useState(false);
  const address = useTicketTabGo();
  const defaults = (useTicketDefaults().data?.entries ?? []).filter((d) => d.specId === spec.specId && d.runId === runId);
  const tally = criteriaTally(spec);
  const bodyId = `spec-card-${runId}-${spec.specId}`;
  return (
    <div className="tk-spec" data-testid={`spec-card-${spec.specId}`}>
      <button type="button" className="tk-spec-head" aria-expanded={open} aria-controls={bodyId} onClick={() => setOpen(!open)}>
        <span className="tk-chev" aria-hidden="true">{open ? "▾" : "▸"}</span>
        <span className="tk-spec-name">Spec <span className="mono">{spec.specId}</span></span>
        {tally && <span className={`tk-spec-tally ${tally.all ? "ok" : "amber"}`} data-testid="spec-card-tally">{tally.words}</span>}
      </button>
      {open && (
        <div className="tk-spec-body" id={bodyId}>
          <div className="tk-spec-goal">{spec.goal}</div>
          {spec.done.map((c, i) => <Criterion key={i} criterion={c} />)}
          <MasterReportText spec={spec} />
          {defaults.map((d) => <Decided key={d.id} entry={d} />)}
          <button type="button" className="tk-link" onClick={() => address.go("specs", { [SPEC_PARAM]: spec.specId })}
            data-testid="spec-card-open">Open the whole spec</button>
        </div>
      )}
    </div>
  );
}

const MARKS: Record<string, [string, string]> = { met: ["ok", "✓"], unmet: ["bad", "✗"] };

function Criterion({ criterion }: { criterion: TicketSeriesCriterion }) {
  const [tone, mark] = MARKS[criterion.verdict ?? ""] ?? ["q", "○"];
  const said = criterion.overrule ? `${criterion.overrule.author} judged it ${criterion.overrule.humanStatus.replace(/_/g, " ")}: ${criterion.overrule.reason}`
    : criterion.note ?? criterion.citation;
  return (
    <div className="tk-crit" data-testid="spec-card-criterion" data-verdict={criterion.verdict ?? "none"}>
      <span className={`tk-mark ${tone}`} aria-label={criterion.verdict ?? "no verdict"}>{mark}</span>
      <div>
        <div>{criterion.text}</div>
        {said && <div className="tk-dim">{said}</div>}
      </div>
    </div>
  );
}

type Sent = { kind: "idle" } | { kind: "sending" } | { kind: "said"; text: string };

/** One default: what was chosen and why; Keep ratifies it, Change overrules it. A design conversation's default is
 *  changed in that conversation, so it offers Keep only. */
function Decided({ entry }: { entry: TicketDefault }) {
  const { project, ticketId } = useTicketDetail();
  const defaults = useTicketDefaults();
  const lacks = useLacksPermission(RUNS_CONTROL, project);
  const [sent, setSent] = useState<Sent>({ kind: "idle" });
  const send = async (verdict: "ratified" | "overruled") => {
    setSent({ kind: "sending" });
    try {
      const answer = await postDefaultVerdicts(project, ticketId, [{ id: entry.id, verdict }]);
      setSent(answer.kind === "refused" ? { kind: "said", text: answer.text } : { kind: "idle" });
    } catch (thrown) {
      setSent({ kind: "said", text: thrown instanceof Error ? thrown.message : String(thrown) });
    }
    defaults.changed();
  };
  const pending = entry.verdict === "pending";
  return (
    <div className="tk-decided" data-testid={`spec-card-default-${entry.id}`}>
      <div>
        <span className="tk-dim">Decided for you: </span>
        {entry.chosen ?? entry.question ?? "a choice without its text"}
        {entry.why && <span className="tk-dim"> {entry.why}</span>}
      </div>
      {pending && !lacks && (
        <span className="tk-decided-acts">
          <button type="button" className="btn sm" disabled={sent.kind === "sending"} onClick={() => void send("ratified")}
            data-testid={`spec-card-keep-${entry.id}`}>Keep</button>
          {!entry.conversation && (
            <button type="button" className="btn sm" disabled={sent.kind === "sending"} onClick={() => void send("overruled")}
              data-testid={`spec-card-change-${entry.id}`}>Change</button>
          )}
        </span>
      )}
      {!pending && <span className="tk-dim" data-testid={`spec-card-verdict-${entry.id}`}>{entry.verdict === "ratified" ? "kept" : "changed"}{entry.verdictBy ? ` by ${entry.verdictBy}` : ""}</span>}
      {sent.kind === "said" && <span className="cp-note bad">{sent.text}</span>}
    </div>
  );
}
