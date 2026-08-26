"use client";

import { useState } from "react";
import type { AccessDocument, AccessGroup, AccessView } from "@/lib/accessApi";
import { groupsOf } from "./derive";
import { PagedRows } from "./PagedRows";
import { RoleChips } from "./RoleChips";

// 2026-08-26-7a51: the Groups pane, which edits the group mapping this installation
// already had — there is no second field with the same meaning. A value somebody has
// carried is picked from the list; one nobody has is typed in.

export function GroupsPane({
  view,
  draft,
  roles,
  onChange,
}: {
  view: AccessView;
  draft: AccessDocument;
  roles: string[];
  onChange: (next: AccessDocument) => void;
}) {
  const [value, setValue] = useState("");
  const groups = groupsOf(view, draft);

  function set(group: AccessGroup, next: string[]) {
    const kept = Object.fromEntries(
      Object.entries(draft.groupRoles).filter(([key]) => key !== group.value && key !== `/${group.value}`),
    );
    onChange({
      ...draft,
      groupRoles: next.length === 0 ? kept : { ...kept, [group.value]: next },
    });
  }

  function add() {
    const typed = value.trim();
    if (typed === "" || draft.groupRoles[typed] !== undefined) return;
    setValue("");
    onChange({ ...draft, groupRoles: { ...draft.groupRoles, [typed]: [] } });
  }

  return (
    <PagedRows
      rows={groups}
      testId="access-groups"
      searchLabel="Search groups"
      searchPlaceholder="Search by group value"
      matchesQuery={(group, query) => group.value.toLowerCase().includes(query)}
      filters={[
        { key: "all", label: "All", matches: () => true },
        { key: "mapped", label: "Mapped", matches: (g) => g.roles.length > 0 },
        { key: "unmapped", label: "Unmapped", matches: (g) => g.roles.length === 0 },
      ]}
      emptyText="No group matches that."
      header={
        <tr>
          <th>
            Group value <span className="mono">· {view.groupClaim}</span>
          </th>
          <th>Carried by</th>
          <th>Grants</th>
        </tr>
      }
      toolbarExtra={
        <div className="adder">
          <input
            className="addfield"
            type="text"
            aria-label="Group value"
            placeholder="group value your directory sends"
            data-testid="access-groups-add-value"
            value={value}
            onChange={(e) => setValue(e.target.value)}
          />
          <button type="button" className="addgo" data-testid="access-groups-add" onClick={add}>
            Add group
          </button>
        </div>
      }
      renderRow={(group) => (
        <tr key={group.value} data-testid={`access-group-${group.value}`}>
          <td className="mono">{group.value}</td>
          <td className="seen">
            {group.carriers} {group.carriers === 1 ? "person" : "people"}
          </td>
          <td>
            <RoleChips
              testId={`access-group-${group.value}-roles`}
              directory={[]}
              granted={group.roles}
              offered={roles}
              grantLabel={`Map ${group.value} onto a role`}
              onGrant={(role) => set(group, [...group.roles, role])}
              onWithdraw={(role) => set(group, group.roles.filter((r) => r !== role))}
            />
          </td>
        </tr>
      )}
    />
  );
}
