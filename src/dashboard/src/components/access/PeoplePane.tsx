"use client";

import { useState } from "react";
import type { AccessDocument, AccessPerson, AccessView } from "@/lib/accessApi";
import { grantedTo, peopleOf } from "./derive";
import { PagedRows } from "./PagedRows";
import { RoleChips } from "./RoleChips";

// 2026-08-26-7a51: the People pane. Everyone this installation has actually seen, plus
// everyone an administrator named by hand — searchable and paged, because a directory's
// worth of callers does not fit on a page.
//
// A row added by hand reads "not signed in yet" rather than a timestamp: that and "signed
// in and holds nothing" are different situations, and the administrator who just typed the
// value is the one who most needs to know which they are looking at.

export function PeoplePane({
  view,
  draft,
  roles,
  onChange,
  onForget,
}: {
  view: AccessView;
  draft: AccessDocument;
  roles: string[];
  onChange: (next: AccessDocument) => void;
  onForget: (id: string) => void;
}) {
  const [value, setValue] = useState("");
  const people = peopleOf(view, draft);

  function withGrants(next: AccessDocument["personGrants"]) {
    onChange({ ...draft, personGrants: next });
  }

  function grant(person: AccessPerson, role: string) {
    const held = grantedTo(draft, person.nameClaim, person.nameValue);
    withGrants([
      ...draft.personGrants.filter(
        (g) => !(g.claim === person.nameClaim && g.value === person.nameValue),
      ),
      { claim: person.nameClaim, value: person.nameValue, roles: [...held, role] },
    ]);
  }

  function withdraw(person: AccessPerson, role: string) {
    const kept = grantedTo(draft, person.nameClaim, person.nameValue).filter((r) => r !== role);
    const others = draft.personGrants.filter(
      (g) => !(g.claim === person.nameClaim && g.value === person.nameValue),
    );
    withGrants(
      kept.length === 0
        ? others
        : [...others, { claim: person.nameClaim, value: person.nameValue, roles: kept }],
    );
  }

  function add() {
    const typed = value.trim();
    if (typed === "") return;
    setValue("");
    // The grant is written against the claim callers are named by TODAY, and stored with
    // it — that is what stops it resolving against a different claim tomorrow.
    withGrants([...draft.personGrants, { claim: view.nameClaim, value: typed, roles: [] }]);
  }

  return (
    <PagedRows
      rows={people}
      testId="access-people"
      searchLabel="Search people"
      searchPlaceholder="Search by name or subject"
      matchesQuery={(person, query) =>
        person.nameValue.toLowerCase().includes(query)
        || (person.subject ?? "").toLowerCase().includes(query)
      }
      filters={[
        { key: "all", label: "All", matches: () => true },
        { key: "granted", label: "Granted here", matches: (p) => p.grantedRoles.length > 0 },
        {
          key: "none",
          label: "No role",
          matches: (p) => p.grantedRoles.length === 0 && p.directoryRoles.length === 0,
        },
      ]}
      emptyText="Nobody matches that."
      header={
        <tr>
          <th>
            Person <span className="mono">· {view.nameClaim}</span>
          </th>
          <th>Last seen</th>
          <th>Roles</th>
          <th />
        </tr>
      }
      toolbarExtra={
        <div className="adder">
          <input
            className="addfield"
            type="text"
            aria-label="Subject"
            placeholder={`${view.nameClaim} value your directory sends`}
            data-testid="access-people-add-value"
            value={value}
            onChange={(e) => setValue(e.target.value)}
          />
          <button type="button" className="addgo" data-testid="access-people-add" onClick={add}>
            Add person
          </button>
        </div>
      }
      renderRow={(person) => (
        <tr key={person.id} data-testid={`access-person-${person.nameValue}`}>
          <td>
            <div className="who-name">{person.nameValue}</div>
            {/* The name-claim value is what a grant is written against; the subject is what
                an environment admin grant matches. Both are kept, and each is labelled. */}
            {person.subject !== null && person.subject !== person.nameValue && (
              <div className="who-sub mono">sub · {person.subject}</div>
            )}
          </td>
          <td className={person.lastSeen === null ? "seen pending" : "seen"}>
            {person.lastSeen === null
              ? "not signed in yet"
              : new Date(person.lastSeen).toLocaleString()}
          </td>
          <td>
            <RoleChips
              testId={`access-person-${person.nameValue}-roles`}
              directory={person.directoryRoles}
              granted={person.grantedRoles}
              offered={roles}
              grantLabel={`Grant a role to ${person.nameValue}`}
              onGrant={(role) => grant(person, role)}
              onWithdraw={(role) => withdraw(person, role)}
            />
          </td>
          <td>
            <button
              type="button"
              className="forget"
              data-testid={`access-person-${person.nameValue}-forget`}
              onClick={() => onForget(person.id)}
            >
              forget
            </button>
          </td>
        </tr>
      )}
    />
  );
}
