"use client";

import type { ReactNode } from "react";
import { sectionDuration, type RunSection, type SectionId } from "./runSections";

// 2026-11-16-6877b: the run as text. A thin strip with one node per section (green done, amber a finding or running,
// red a stop, hollow not reached) above seven sections that all start closed; a node opens its section. An open
// section shows its steps as timestamped lines; "Checked against the spec" shows the criteria with their overrule.

interface Props {
  sections: RunSection[];
  open: ReadonlySet<SectionId>;
  onToggle: (id: SectionId) => void;
  /** The criteria with their overrule control, drawn inside the check section. */
  check: ReactNode;
}

export function RunProgressStrip({ sections, open, onToggle }: Omit<Props, "check">) {
  return (
    <div className="rl-strip" role="group" aria-label="Where the run is" data-testid="run-strip">
      {sections.map((s) => (
        <button key={s.id} type="button" className={`rl-node ${s.state}`} aria-pressed={open.has(s.id)}
          title={`${s.title}: ${s.summary}`} onClick={() => onToggle(s.id)} data-testid={`run-node-${s.id}`}>
          <span className="rl-node-mark" aria-hidden="true" />
          <span className="rl-node-label">{s.short}</span>
        </button>
      ))}
    </div>
  );
}

export function RunSectionLog({ sections, open, onToggle, check }: Props) {
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
                {s.id === "check" ? check : s.lines.length === 0 ? <div className="rl-line rl-dim">Nothing ran here.</div> : s.lines.map((l) => (
                  <div key={l.stepIndex} className={`rl-line ${l.state}`} data-testid="run-log-line">
                    <span className="rl-at">{l.at ?? ""}</span>
                    <span>
                      {l.text}
                      {l.detail && <span className="rl-dim">: {l.detail}</span>}
                    </span>
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
