"use client";

import { useEffect, useState } from "react";
import type { AccessDocument } from "@/lib/accessApi";
import { refusalIn } from "@/lib/apiResponse";
import { RefusalSurface } from "@/components/shell/RefusalSurface";
import { ClaimsPane } from "./ClaimsPane";
import { GroupsPane } from "./GroupsPane";
import { PeoplePane } from "./PeoplePane";
import { RolesPane } from "./RolesPane";
import { groupsOf, peopleOf } from "./derive";
import { useAccess } from "./useAccess";

// 2026-08-26-7a51: who may do what — People, Groups, Roles and the claim names, four panes
// over ONE document. Edited as a draft and saved whole, which is the Config Studio's own
// idiom and also the only correct shape: the server binds a settings body onto a fresh
// model, so anything the surface does not send reverts to a default.

type Pane = "people" | "groups" | "roles" | "claims";

export function AccessStudio() {
  const { view, loading, error, saving, saveError, save, forget } = useAccess();
  const [pane, setPane] = useState<Pane>("people");
  const [draft, setDraft] = useState<AccessDocument | null>(null);

  // The draft restarts from persisted truth on every fresh load — the initial one, and the
  // reload a save or a removal answers with.
  useEffect(() => setDraft(view?.document ?? null), [view]);

  const loadRefusal = refusalIn(error);
  if (loadRefusal) return <RefusalSurface refusal={loadRefusal} surface="the access settings" />;
  if (loading || view === null || draft === null)
    return <div className="mock-shell mock-config mock-access empty">Loading…</div>;

  const dirty = JSON.stringify(draft) !== JSON.stringify(view.document);
  const roles = view.roles.map((role) => role.name);
  const people = peopleOf(view, draft);
  const groups = groupsOf(view, draft);

  return (
    <div className="mock-shell mock-config mock-access" data-testid="access-studio">
      <main className="main">
        <div className="m-head">
          <div className="mt">
            <h1>Who may do what</h1>
            <div className="msub">people, groups, roles and the claims they are read from</div>
          </div>
          <button
            type="button"
            className="btn primary"
            data-testid="access-save"
            disabled={!dirty || saving}
            onClick={() => void save(draft)}
          >
            {saving ? "Saving…" : "Save changes"}
          </button>
        </div>

        <p className="legend">
          <span className="chip chip-directory">
            <span className="src">group</span>operator
          </span>{" "}
          from your directory
          <span className="sep">·</span>
          <span className="chip chip-here">
            <span className="src">granted</span>admin
          </span>{" "}
          granted here
        </p>

        {view.nameClaimIsSelfAsserted && (
          <p className="warn" data-testid="access-nameclaim-warning">
            Callers are named by <b>{view.nameClaim}</b>, not <b>sub</b>. A value like that is
            self-asserted in common directory configurations — somebody who can change their
            own can claim a grant written for someone else.
          </p>
        )}
        {saveError && (
          <p className="warn" data-testid="access-save-error" style={{ color: "var(--bad)" }}>
            {saveError.message}
          </p>
        )}
        {view.findings.map((finding) => (
          <p className="warn" key={finding} data-testid="access-finding">
            {finding}
          </p>
        ))}

        <div className="tabs" role="tablist" aria-label="Access settings">
          <Tab id="people" pane={pane} onSelect={setPane} label="People" count={people.length} />
          <Tab id="groups" pane={pane} onSelect={setPane} label="Groups" count={groups.length} />
          <Tab id="roles" pane={pane} onSelect={setPane} label="Roles" count={view.roles.length} />
          <Tab id="claims" pane={pane} onSelect={setPane} label="Claim names" />
        </div>

        {pane === "people" && (
          <PeoplePane
            view={view}
            draft={draft}
            roles={roles}
            onChange={setDraft}
            onForget={(id) => void forget(id)}
          />
        )}
        {pane === "groups" && (
          <GroupsPane view={view} draft={draft} roles={roles} onChange={setDraft} />
        )}
        {pane === "roles" && <RolesPane view={view} people={people} groups={groups} />}
        {pane === "claims" && (
          <ClaimsPane draft={draft} nameClaim={view.nameClaim} onChange={setDraft} />
        )}
      </main>
    </div>
  );
}

function Tab({
  id,
  pane,
  onSelect,
  label,
  count,
}: {
  id: Pane;
  pane: Pane;
  onSelect: (pane: Pane) => void;
  label: string;
  count?: number;
}) {
  return (
    <button
      type="button"
      className="tab"
      role="tab"
      aria-selected={pane === id}
      data-testid={`access-tab-${id}`}
      onClick={() => onSelect(id)}
    >
      {label}
      {count !== undefined && <span className="n">{count}</span>}
    </button>
  );
}
