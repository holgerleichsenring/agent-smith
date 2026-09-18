"use client";

import type { SpecDialogProject, SpecDialogSession } from "@/types/spec-dialog";

// 2026-09-15-cb3e: WHAT THE AGENT IS GROUNDED IN — the project, the repositories and the
// templates a turn of this conversation may read. It is what decides whether the agent's
// statements are worth anything: a proposal is only as good as the code it was allowed to
// look at. 2026-09-15-6d9c: the FIRST state of the right-hand column, shown until the
// conversation has proposed something — DialogColumn decides which state is current.
//
// 2026-09-17-042ef: a repository and a template are each ONE MARK, the same mark the Projects
// page counts a project's facts in — a bulleted list of em-dashed sentences was the one place
// on this page that looked like nothing else in the product. A template's mark names it and
// then says where it comes from: "repo@revision", or a plain repository where the scope
// pinned no revision.

export function DialogScopePanel({
  session,
  projects,
}: {
  session: SpecDialogSession | null;
  projects: SpecDialogProject[];
}) {
  return (
    <div data-testid="dialog-scope">
      {/* The Scope tab above names this pane; a heading repeating it was the same noise the
          filed pane carried, one tab over. */}
      {session ? (
        <>
          <p className="ec-sub mb-2">What this conversation may read.</p>
          <Grounding project={session.scope} />
        </>
      ) : (
        <>
          <p className="ec-sub mb-2">
            No conversation is open on this page yet. A new one can be grounded in:
          </p>
          {projects.length === 0 ? (
            <p data-testid="dialog-scope-none" className="dsh-body text-ink">
              No project is configured, so there is nothing to design against yet.
            </p>
          ) : (
            projects.map((project) => <Grounding key={project.name} project={project} />)
          )}
        </>
      )}
    </div>
  );
}

function Grounding({ project }: { project: SpecDialogProject }) {
  return (
    <div data-testid={`dialog-scope-project-${project.name}`} className="mb-3">
      <div className="ec-name given">{project.name}</div>
      <div className="fl mt-2">Repositories</div>
      {project.repos.length === 0 ? (
        <div className="ec-sub">no repositories</div>
      ) : (
        <div className="ec-marks">
          {project.repos.map((repo) => (
            <span key={repo} data-testid={`dialog-scope-repo-${repo}`} className="ec-mark given">
              {repo}
            </span>
          ))}
        </div>
      )}
      <div className="fl mt-2">Templates</div>
      {project.templates.length === 0 ? (
        <div className="ec-sub">no templates declared</div>
      ) : (
        <div className="ec-marks">
          {project.templates.map((template) => (
            <span
              key={template.name}
              data-testid={`dialog-scope-template-${template.name}`}
              className="ec-mark given"
            >
              <span className="mn">{template.name}</span>
              {template.revision ? `${template.repo}@${template.revision}` : template.repo}
            </span>
          ))}
        </div>
      )}
    </div>
  );
}
