"use client";

import type { ReactNode } from "react";
import { CitedList } from "./CitedList";
import { InlineText } from "./LogText";
import type { DeliveredAccount, DeliveredRow, SpecRecordModel } from "./specRecord";

// 2026-11-18-e06bc: the executed spec as labelled lines — goal, scope, steps (id — action), tests, done, the other keys
// it carries, and the outcome: each delivered criterion on one line with its state and what it cites behind "n files ›".

function Block({ label, children, testId }: { label: string; children: ReactNode; testId?: string }) {
  return (
    <div className="rl-spec-row" data-testid={testId}>
      <span className="rl-spec-label">{label}</span>
      <div className="rl-spec-val">{children}</div>
    </div>
  );
}

const MARK: Record<string, { glyph: string; word: string; cls: string }> = {
  met: { glyph: "✓", word: "proven", cls: "done" },
  unmet: { glyph: "✗", word: "not met", cls: "bad" },
  na: { glyph: "~", word: "not applicable", cls: "pending" },
};

function Row({ row }: { row: DeliveredRow }) {
  if (row.kind === "line") return <div className="rl-dim" data-testid="spec-delivered-line"><InlineText text={row.text} /></div>;
  const mark = MARK[row.mark];
  const command = row.note === "verified by command";
  return (
    <div className={`rl-delivered ${mark.cls}`} data-testid="spec-delivered-criterion" data-mark={row.mark}>
      <span className="rl-delivered-mark">{mark.glyph}</span>
      <span className="rl-delivered-text">
        <InlineText text={row.text} />
        {row.note && !command && <span className="rl-dim"> — <InlineText text={row.note} /></span>}
      </span>
      <span className="rl-delivered-state">{mark.word}{command ? " by command" : ""}</span>
      {row.citation && <span className="rl-delivered-cited"><CitedList text={row.citation} /></span>}
    </div>
  );
}

function Account({ account }: { account: DeliveredAccount }) {
  return (
    <div data-testid="spec-delivered-account">
      {account.repo && <div className="rl-strong">{account.repo}</div>}
      {account.rows.map((r, i) => <Row key={i} row={r} />)}
    </div>
  );
}

export function SpecRecordView({ spec }: { spec: SpecRecordModel }) {
  return (
    <div className="rl-spec" data-testid="spec-record-view">
      {spec.goal && <Block label="goal" testId="spec-record-goal"><InlineText text={spec.goal} /></Block>}
      {spec.scope.map((b) => (
        <Block key={b.label} label={b.label} testId={`spec-record-${b.label.replace(/\s+/g, "-")}`}>
          {b.lines.map((l, i) => <div key={i}><InlineText text={l} /></div>)}
        </Block>
      ))}
      {spec.steps.length > 0 && (
        <Block label="steps" testId="spec-record-steps">
          {spec.steps.map((s) => (
            <div key={s.id} className="rl-spec-step"><span className="rl-dim">{s.id}</span><span><InlineText text={s.action} /></span></div>
          ))}
        </Block>
      )}
      {spec.tests.length > 0 && <Block label="tests" testId="spec-record-tests">{spec.tests.map((t, i) => <div key={i}><InlineText text={t} /></div>)}</Block>}
      {spec.done.length > 0 && (
        <Block label="done" testId="spec-record-done">
          <ol className="rl-num">{spec.done.map((d, i) => <li key={i}><span className="rl-num-n">{i + 1}.</span><span><InlineText text={d} /></span></li>)}</ol>
        </Block>
      )}
      {spec.other.map((b) => (
        <Block key={b.label} label={b.label}>{b.lines.map((l, i) => <div key={i} className="rl-dim"><InlineText text={l} /></div>)}</Block>
      ))}
      {spec.delivered.length > 0 && (
        <Block label="delivered" testId="spec-record-delivered">{spec.delivered.map((a, i) => <Account key={i} account={a} />)}</Block>
      )}
      {spec.run && <Block label="run"><span className="rl-dim">{spec.run}</span></Block>}
    </div>
  );
}
