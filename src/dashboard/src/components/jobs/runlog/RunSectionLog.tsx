"use client";

import type { ReactNode } from "react";
import { sectionDuration, type RunSection, type SectionId } from "./runSections";
import { VerifyPassLines } from "./VerifyPassLines";

// 2026-11-16-6877b: the run as text — seven sections that start closed. An open section shows its steps as timestamped
// lines; "Checked against the spec" shows what it is given. 2026-11-17-6d9eb: the ticket page's inline log draws this;
// the run page draws RunLog, sectioned by spec.

interface Props {
  sections: RunSection[];
  open: ReadonlySet<SectionId>;
  onToggle: (id: SectionId) => void;
  /** What a section draws instead of step lines: the criteria with their overrule control in the check section;
   *  2026-11-17-d6fbb: the ticket, the spec and plan, and the outcome in their own sections. */
  slots: Partial<Record<SectionId, ReactNode>>;
}

export function RunSectionLog({ sections, open, onToggle, slots }: Props) {
  return (
    <div className="rl-sections" data-testid="run-sections">
      {sections.map((s) => {
        const isOpen = open.has(s.id);
        return (
          <section key={s.id} className={`rl-sec ${s.state}`} data-testid={`run-section-${s.id}`} data-state={s.state}>
            <button type="button" className="rl-sec-head" aria-expanded={isOpen} aria-controls={`run-sec-${s.id}`} onClick={() => onToggle(s.id)}>
              <span className="rl-chev" aria-hidden="true">{isOpen ? "▾" : "▸"}</span>
              <span className="rl-sec-mark" aria-hidden="true" />
              <span className="rl-sec-title">{s.title}</span>
              <span className="rl-sec-sum">{s.summary}</span>
              <span className="rl-sec-time">{sectionDuration(s.seconds)}</span>
            </button>
            {isOpen && (
              <div className="rl-sec-body" id={`run-sec-${s.id}`}>
                {slots[s.id] !== undefined ? <div className="rl-slot">{slots[s.id]}</div> : s.lines.length === 0 ? <div className="rl-line rl-dim">Nothing ran here.</div> : s.lines.map((l) => (
                  <div key={l.stepIndex} className={`rl-line ${l.state}`} data-testid="run-log-line">
                    <span className="rl-at">{l.at ?? ""}</span>
                    {l.verify ? (
                      <div>
                        {l.text} <span className="rl-dim">{l.verify.result}</span>
                        <VerifyPassLines pass={l.verify} />
                      </div>
                    ) : (
                      <span>
                        {l.text}
                        {l.detail && <span className="rl-dim">: {l.detail}</span>}
                      </span>
                    )}
                  </div>
                ))}
              </div>
            )}
          </section>
        );
      })}
    </div>
  );
}
