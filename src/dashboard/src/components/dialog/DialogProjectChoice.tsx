"use client";

import type { SpecDialogProject } from "@/types/spec-dialog";

// 2026-09-23-6e3f: with nothing open, the exchange column IS the choice. The rule that a
// conversation needs a project was already there — what was wrong is where the two halves sat:
// the refusal was a disabled text box in the middle of the page and its cause was a select at
// the top of the left column, so a person read a dead box under "New conversation" and
// concluded something had been swallowed. The select is here now, centred, with no composer
// beside it to refuse a message; picking one is what puts the composer there.
//
// Three states, which the project list alone cannot tell apart because it is empty in all of
// them. A read still in flight says so. A read that FAILED leaves the same empty list for ever
// and must not wear the pending line — the failure itself is named by the page's own banner
// above. And an installation with nothing configured is told that, with no select to hunt for.
// An installation with exactly one project resolves it without a choice and never renders this.

/** What the page knows about the configured projects: the read is out, it failed, or it answered. */
export type ProjectsRead = "pending" | "failed" | "read";

export function DialogProjectChoice({
  projects,
  read,
  picked,
  onPicked,
  reason,
}: {
  projects: SpecDialogProject[];
  read: ProjectsRead;
  picked: string;
  onPicked: (project: string) => void;
  /** 2026-09-25-8e51a: why a ticket did not name one project — several, none, or one that could
   *  not be answered for from a ticket at all. A reason, shown where the choice is made. */
  reason?: string | null;
}) {
  return (
    <div
      data-testid="dialog-project-choice"
      className="flex flex-col items-center gap-3 py-12 text-center"
    >
      {reason && (
        <p data-testid="dialog-project-choice-reason" className="dsh-body text-body">
          {reason}
        </p>
      )}
      {read === "pending" && (
        <p data-testid="dialog-project-choice-loading" className="dsh-body text-body">
          Reading the configured projects…
        </p>
      )}
      {read === "failed" && (
        <p data-testid="dialog-project-choice-failed" className="dsh-body text-body">
          The configured projects could not be read, so there is nothing to choose from yet.
          What went wrong is reported at the top of this page.
        </p>
      )}
      {read === "read" && projects.length === 0 && (
        <p data-testid="dialog-project-choice-none" className="dsh-body text-body">
          No project is configured, so there is nothing to design against yet. Configure one in
          Config Studio and a conversation can be grounded in it.
        </p>
      )}
      {read === "read" && projects.length > 0 && (
        <>
          <p className="dsh-h3 text-ink">Which project is this about?</p>
          <p className="dsh-body text-body">
            A conversation reads one project&apos;s repositories before it answers, so this is
            what it will be grounded in.
          </p>
          {/* The label is the control's own: nothing above it names the select, and a person
              tabbing here hears what they are choosing. */}
          <select
            data-testid="dialog-choice-project"
            aria-label="Project"
            value={picked}
            onChange={(event) => onPicked(event.target.value)}
            className="d-input max-w-xs"
          >
            <option value="">pick a project…</option>
            {projects.map((project) => (
              <option key={project.name} value={project.name}>
                {project.name}
              </option>
            ))}
          </select>
        </>
      )}
    </div>
  );
}
