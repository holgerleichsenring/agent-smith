"use client";

import { useState } from "react";
import type { AccessDocument } from "@/lib/accessApi";
import { permissionAreas } from "./derive";

// 2026-09-14-91ad: composing a role out of the permission catalog, which until now was a
// refusal at the save and a config import as the only way in.
//
// It edits document.roles — the STORED bundle — and never the matrix above it. 2026-08-26-7a51
// chose to carry the document alongside the derived panes precisely so a surface could not
// rebuild it from its own rows: what the matrix renders is the bundle MINUS whatever the
// server's catalog did not recognise, so an editor reading rows would silently drop a legacy
// role's unknown permission the first time somebody touched an unrelated one.
//
// Only roles configured HERE appear. The three built-in bundles are not in this document at
// all, which is what makes "a custom role is additive" a shape rather than a promise.

export function RoleEditor({
  draft,
  permissions,
  onChange,
}: {
  draft: AccessDocument;
  permissions: string[];
  onChange: (next: AccessDocument) => void;
}) {
  const [name, setName] = useState("");
  const configured = Object.keys(draft.roles).sort();
  const typed = name.trim();

  function withRoles(roles: Record<string, string[]>) {
    onChange({ ...draft, roles });
  }

  function add() {
    if (typed === "" || typed in draft.roles) return;
    withRoles({ ...draft.roles, [typed]: [] });
    setName("");
  }

  function toggle(role: string, permission: string) {
    const held = draft.roles[role] ?? [];
    withRoles({
      ...draft.roles,
      [role]: held.includes(permission)
        ? held.filter((p) => p !== permission)
        : [...held, permission],
    });
  }

  function remove(role: string) {
    withRoles(Object.fromEntries(Object.entries(draft.roles).filter(([name]) => name !== role)));
  }

  return (
    <section data-testid="access-role-editor">
      <h3>Roles configured here</h3>
      <p className="help">
        A role composed here stands beside the built-in three and never replaces one. Removing
        it is refused while anybody this installation can see still holds it.
      </p>
      {configured.map((role) => (
        <div key={role} className="rolecard" data-testid={`access-role-edit-${role}`}>
          <div className="rolehead">
            <b>{role}</b>
            <button
              type="button"
              className="btn"
              data-testid={`access-role-remove-${role}`}
              onClick={() => remove(role)}
            >
              Remove
            </button>
          </div>
          {permissionAreas(permissions).map(([area, names]) => (
            <fieldset key={area}>
              <legend>{area}</legend>
              {names.map((permission) => (
                <label key={permission}>
                  <input
                    type="checkbox"
                    checked={(draft.roles[role] ?? []).includes(permission)}
                    onChange={() => toggle(role, permission)}
                    data-testid={`access-role-${role}-${permission}`}
                  />
                  {permission}
                </label>
              ))}
            </fieldset>
          ))}
        </div>
      ))}
      <div className="roleadd">
        <input
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="new role name"
          aria-label="new role name"
          data-testid="access-role-new-name"
        />
        <button
          type="button"
          className="btn"
          data-testid="access-role-add"
          disabled={typed === "" || typed in draft.roles}
          onClick={add}
        >
          Add role
        </button>
      </div>
    </section>
  );
}
