"use client";

import type {
  SpecDialogProject,
  SpecDialogSession,
  SpecDialogSessionTicket,
} from "@/types/spec-dialog";

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
  starting,
}: {
  session: SpecDialogSession | null;
  projects: SpecDialogProject[];
  /** 2026-09-27-481bd: what the NEXT conversation is already settled on — the project it will be
   *  grounded in, and the ticket it starts from. Absent while nothing has been chosen. */
  starting?: { ticketId: string; title: string; kind?: string | null } | null;
}) {
  return (
    <div data-testid="dialog-scope">
      {/* The Scope tab above names this pane; a heading repeating it was the same noise the
          filed pane carried, one tab over. */}
      {session ? (
        <>
          <p className="ec-sub mb-2">What this conversation may read.</p>
          {/* 2026-09-27-481bc: the ticket comes FIRST, because it is the requirement the rest is
              read against — and because it was the one thing the model read that a person could
              not. It is the SEEDED copy: the conversation reasons from what it was given, and a
              ticket that has since changed is exactly where the two differ. */}
          {session.ticket && <BoundTicket ticket={session.ticket} />}
          <Grounding project={session.scope} />
        </>
      ) : (
        <>
          {/* Until something is chosen this lists the candidates; once a project is settled it
              is a statement about the conversation that is about to start, and saying "no
              conversation is open" there reads as though nothing had been chosen at all. */}
          <p className="ec-sub mb-2">
            {projects.length === 1
              ? "The conversation you are about to start will be grounded in:"
              : "No conversation is open on this page yet. A new one can be grounded in:"}
          </p>
          {starting && (
            <div data-testid="dialog-scope-starting" className="mb-3">
              <div className="fl">Ticket</div>
              <div className="ec-marks ec-sub items-center">
                <span className="ec-mark filed">{starting.ticketId}</span>
                {starting.kind && <span>{starting.kind}</span>}
                <span className="min-w-0 truncate text-ink">{starting.title}</span>
              </div>
            </div>
          )}
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

function BoundTicket({ ticket }: { ticket: SpecDialogSessionTicket }) {
  return (
    <div data-testid="dialog-scope-ticket" className="mb-3">
      <div className="fl">Ticket</div>
      <div className="ec-marks ec-sub items-center">
        <span className="ec-mark filed">{ticket.ticketId}</span>
        <span className="min-w-0 truncate text-ink">{ticket.title}</span>
      </div>
      <div className="ec-sub mt-1">
        read {new Date(ticket.readAt).toLocaleString()}
        {ticket.truncated && " · longer than this conversation carries"}
      </div>
      {/* The text the turn was actually seeded with, whitespace kept: a ticket body is written
          with line breaks that carry meaning, and collapsing them would show a different text
          than the one the model read. */}
      <pre
        data-testid="dialog-scope-ticket-text"
        className="ec-sub mt-2 max-h-72 overflow-auto whitespace-pre-wrap break-words"
      >
        {ticket.text}
      </pre>
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
