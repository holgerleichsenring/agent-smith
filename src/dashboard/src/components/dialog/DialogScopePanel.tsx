"use client";

import type { SpecDialogProject, SpecDialogSession } from "@/types/spec-dialog";

// 2026-09-15-cb3e: WHAT THE AGENT IS GROUNDED IN — the project, the repositories and the
// templates a turn of this conversation may read. It is what decides whether the agent's
// statements are worth anything: a proposal is only as good as the code it was allowed to
// look at. 2026-09-15-6d9c: the FIRST state of the right-hand column, shown until the
// conversation has proposed something — DialogColumn decides which state is current.

export function DialogScopePanel({
  session,
  projects,
}: {
  session: SpecDialogSession | null;
  projects: SpecDialogProject[];
}) {
  return (
    <aside data-testid="dialog-scope" className="rounded border border-stone-200 p-3">
      <h2 className="dsh-h3 mb-1 font-semibold text-stone-800">Scope</h2>
      {session ? (
        <>
          <p className="mb-2 text-xs text-[var(--color-ink-mid)]">
            What this conversation may read.
          </p>
          <Grounding project={session.scope} />
        </>
      ) : (
        <>
          <p className="mb-2 text-xs text-[var(--color-ink-mid)]">
            No conversation is open on this page yet. A new one can be grounded in:
          </p>
          {projects.length === 0 ? (
            <p data-testid="dialog-scope-none" className="dsh-body text-stone-700">
              No project is configured, so there is nothing to design against yet.
            </p>
          ) : (
            projects.map((project) => <Grounding key={project.name} project={project} />)
          )}
        </>
      )}
    </aside>
  );
}

function Grounding({ project }: { project: SpecDialogProject }) {
  return (
    <div data-testid={`dialog-scope-project-${project.name}`} className="mb-3">
      <div className="dsh-body font-semibold text-stone-800">{project.name}</div>
      <Facts label="Repositories" items={project.repos} empty="no repositories" />
      <Facts
        label="Templates"
        items={project.templates.map((template) =>
          `${template.name} — ${template.repo}${template.revision ? ` @ ${template.revision}` : ""}`)}
        empty="no templates declared"
      />
    </div>
  );
}

function Facts({ label, items, empty }: { label: string; items: string[]; empty: string }) {
  return (
    <div className="mt-1">
      <div className="dsh-label uppercase tracking-wide text-[var(--color-ink-mid)]">{label}</div>
      {items.length === 0 ? (
        <div className="dsh-label text-[var(--color-ink-mid)]">{empty}</div>
      ) : (
        <ul className="dsh-label ml-4 list-disc text-stone-700">
          {items.map((item) => (
            <li key={item}>{item}</li>
          ))}
        </ul>
      )}
    </div>
  );
}
