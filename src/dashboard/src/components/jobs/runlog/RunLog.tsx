"use client";

import { Fragment, useMemo, type ReactNode } from "react";
import { EventType, type RunEvent, type VerifyPassRecordedEvent } from "@/types/hub-events";
import { costWords, llmWords, stepTime, type LogSection, type LogStep } from "./runLogSections";
import { VerifyPassLines } from "./VerifyPassLines";

// 2026-11-17-6d9eb: the sectioned log in the approved mock's columns, hairlines and text colour only. A section line
// says its state by its dot, its name and its steps · llm · cost · time; open, it lists its step lines (name, llm,
// cost, time), the steps the run keeps out of the story behind one "n mechanics steps" line, and what the section
// read or produced. Every step the run reached opens in place.

export interface RunLogProps {
  sections: LogSection[];
  events: RunEvent[];
  open: ReadonlySet<string>;
  onToggle: (sectionId: string) => void;
  /** The step whose detail is open, by step index. */
  openStep: number | null;
  onToggleStep: (stepIndex: number) => void;
  mechanicsOpen: ReadonlySet<string>;
  onToggleMechanics: (sectionId: string) => void;
  /** What a section draws besides its steps: the ticket, the plan, a spec's decisions, the criteria, the report. */
  body: (section: LogSection) => ReactNode;
  /** The open step's detail. */
  detail: (step: LogStep) => ReactNode;
}

export function RunLog(props: RunLogProps) {
  const passes = useMemo(() => {
    const map = new Map<number, VerifyPassRecordedEvent[]>();
    for (const e of props.events) {
      if (e.type !== EventType.VerifyPassRecorded || e.originStepIndex == null) continue;
      map.set(e.originStepIndex, [...(map.get(e.originStepIndex) ?? []), e]);
    }
    return map;
  }, [props.events]);
  return (
    <div className="rl-log" data-testid="run-sections">
      {props.sections.map((s) => {
        const isOpen = props.open.has(s.id);
        return (
          <section key={s.id} className={`rl-sec ${s.state}`} data-testid={`run-section-${s.id}`} data-state={s.state} data-kind={s.kind}>
            <button type="button" className="rl-sec-head" aria-expanded={isOpen} aria-controls={`run-sec-${s.id}`} onClick={() => props.onToggle(s.id)}>
              <span className="rl-chev" aria-hidden="true">{isOpen ? "▾" : "▸"}</span>
              <span className="rl-sec-mark" aria-hidden="true" />
              <span className="rl-sec-title">{s.title}</span>
              <span className="rl-sec-sum" data-testid={`run-section-meta-${s.id}`}>{s.meta}</span>
            </button>
            {isOpen && (
              <div className="rl-sec-body" id={`run-sec-${s.id}`}>
                <Steps section={s} passes={passes} {...props} />
                {props.body(s)}
              </div>
            )}
          </section>
        );
      })}
    </div>
  );
}

function Steps({ section, passes, openStep, onToggleStep, mechanicsOpen, onToggleMechanics, detail }:
  RunLogProps & { section: LogSection; passes: Map<number, VerifyPassRecordedEvent[]> }) {
  const hidden = section.steps.filter((s) => !s.story);
  const holdsOpen = hidden.some((s) => s.row.stepIndex === openStep);
  const showAll = mechanicsOpen.has(section.id) || holdsOpen;
  const firstHidden = hidden[0]?.row.stepIndex;
  return (
    <>
      {section.steps.map((step) => (
        <Fragment key={step.row.stepIndex}>
          {step.row.stepIndex === firstHidden && (
            <button type="button" className="rl-mech" data-testid={`run-mechanics-${section.id}`} aria-expanded={showAll}
              onClick={() => onToggleMechanics(section.id)}>
              <span className="rl-chev" aria-hidden="true">{showAll ? "▾" : "▸"}</span>
              {showAll ? "hide the mechanics" : hidden.length === 1 ? "1 mechanics step" : `${hidden.length} mechanics steps`}
            </button>
          )}
          {(step.story || showAll) && (
            <StepLine step={step} open={openStep === step.row.stepIndex} onToggle={() => onToggleStep(step.row.stepIndex)}
              passes={passes.get(step.row.stepIndex) ?? []} detail={detail} />
          )}
        </Fragment>
      ))}
    </>
  );
}

function StepLine({ step, open, onToggle, passes, detail }: {
  step: LogStep; open: boolean; onToggle: () => void; passes: VerifyPassRecordedEvent[]; detail: (step: LogStep) => ReactNode;
}) {
  const { row } = step;
  return (
    <div className={`rl-step ${step.state}`} data-testid={`run-step-${row.stepIndex}`} data-state={step.state} id={`step-${row.stepIndex}`}>
      <button type="button" className="rl-step-line" aria-expanded={open} onClick={onToggle} data-testid="run-log-line">
        <span className="rl-chev" aria-hidden="true">{open ? "▾" : "▸"}</span>
        <span className="rl-step-name">{step.label}</span>
        <span className="rl-step-llm">{llmWords(row.llmCalls)}</span>
        <span className="rl-step-cost">{costWords(row.costUsd)}</span>
        <span className="rl-step-time">{row.planned ? "" : stepTime(row.durationSeconds)}</span>
      </button>
      {passes.map((p, i) => <div key={i} className="rl-step-pass"><VerifyPassLines pass={p} /></div>)}
      {open && <div className="rl-step-detail" data-testid={`run-step-detail-${row.stepIndex}`}>{detail(step)}</div>}
    </div>
  );
}
