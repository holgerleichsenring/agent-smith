"use client";

import { useState } from "react";
import type { AccessDocument, AccessGroup, AccessView } from "@/lib/accessApi";
import { groupsOf } from "./derive";
import { PagedRows } from "./PagedRows";
import { RoleChips } from "./RoleChips";
import { useT } from "@/hooks/useT";

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
  const t = useT();
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
      searchLabel={t("access.groups.search")}
      searchPlaceholder={t("access.groups.searchPlaceholder")}
      matchesQuery={(group, query) => group.value.toLowerCase().includes(query)}
      filters={[
        { key: "all", label: t("access.filter.all"), matches: () => true },
        { key: "mapped", label: t("access.groups.mapped"), matches: (g) => g.roles.length > 0 },
        { key: "unmapped", label: t("access.groups.unmapped"), matches: (g) => g.roles.length === 0 },
      ]}
      emptyText={t("access.groups.empty")}
      header={
        <tr>
          <th>
            {t("access.groups.value")} <span className="mono">· {view.groupClaim}</span>
          </th>
          <th>{t("access.groups.carriedBy")}</th>
          <th>{t("access.groups.grants")}</th>
        </tr>
      }
      toolbarExtra={
        <div className="adder">
          <input
            className="addfield"
            type="text"
            aria-label={t("access.groups.value")}
            placeholder={t("access.groups.valuePlaceholder")}
            data-testid="access-groups-add-value"
            value={value}
            onChange={(e) => setValue(e.target.value)}
          />
          <button type="button" className="addgo" data-testid="access-groups-add" onClick={add}>
            {t("access.groups.add")}
          </button>
        </div>
      }
      renderRow={(group) => (
        <tr key={group.value} data-testid={`access-group-${group.value}`}>
          <td className="mono">{group.value}</td>
          <td className="seen">
            {t("access.groups.people", { count: group.carriers })}
          </td>
          <td>
            <RoleChips
              testId={`access-group-${group.value}-roles`}
              directory={[]}
              granted={group.roles}
              offered={roles}
              grantLabel={t("access.groups.mapOnto", { group: group.value })}
              onGrant={(role) => set(group, [...group.roles, role])}
              onWithdraw={(role) => set(group, group.roles.filter((r) => r !== role))}
            />
          </td>
        </tr>
      )}
    />
  );
}
