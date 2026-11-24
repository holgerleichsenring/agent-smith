"use client";

import type { AccessDocument, AccessGroup, AccessPerson, AccessView } from "@/lib/accessApi";
import { groupsGranting, peopleHolding, permissionAreas } from "./derive";
import { RoleEditor } from "./RoleEditor";
import { withValues } from "./withValues";
import { useT } from "@/hooks/useT";

// 2026-08-26-7a51: the Roles pane — the roles this installation offers, what each holds,
// and who carries it, above the full permission matrix.
//
// 2026-09-14-91ad: the matrix stays a read-only overview and the editing happens below it,
// over the stored document. The two are deliberately different things: the matrix shows what
// each role RESOLVES to, which for a legacy bundle is less than what it says.

export function RolesPane({
  view,
  draft,
  people,
  groups,
  onChange,
}: {
  view: AccessView;
  draft: AccessDocument;
  people: AccessPerson[];
  groups: AccessGroup[];
  onChange: (next: AccessDocument) => void;
}) {
  const t = useT();
  const areas = permissionAreas(view.permissions);
  return (
    <div data-testid="access-roles">
      <div className="rolecards">
        {view.roles.map((role) => (
          <div key={role.name} className="rolecard" data-testid={`access-role-${role.name}`}>
            <h3>{role.name}</h3>
            <p className="carried">
              {withValues(t("access.roles.permissionsOf"), {
                held: <b>{role.permissions.length}</b>,
                total: view.permissions.length,
              })}
              <br />
              {withValues(t("access.roles.carriedBy"), {
                people: counted(t("access.roles.people", { count: peopleHolding(people, role.name) }), peopleHolding(people, role.name)),
                groups: counted(t("access.roles.groups", { count: groupsGranting(groups, role.name) }), groupsGranting(groups, role.name)),
              })}
              {!role.builtIn && (
                <>
                  <br />
                  <span data-testid={`access-role-${role.name}-custom`}>
                    {t("access.roles.custom")}
                  </span>
                </>
              )}
            </p>
          </div>
        ))}
      </div>
      <div className="tablewrap">
        <table className="matrix">
          <thead>
            <tr>
              <th>{t("access.roles.permission")}</th>
              {view.roles.map((role) => (
                <th key={role.name} className="role">
                  {role.name}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {areas.map(([area, permissions]) => (
              <Area key={area} area={area} permissions={permissions} view={view} />
            ))}
          </tbody>
        </table>
      </div>
      <RoleEditor draft={draft} permissions={view.permissions} onChange={onChange} />
    </div>
  );
}

function Area({
  area,
  permissions,
  view,
}: {
  area: string;
  permissions: string[];
  view: AccessView;
}) {
  const t = useT();
  return (
    <>
      <tr className="grp">
        <td colSpan={view.roles.length + 1}>{area}</td>
      </tr>
      {permissions.map((permission) => (
        <tr key={permission} data-testid={`access-permission-${permission}`}>
          <td className="perm">{permission}</td>
          {view.roles.map((role) => (
            <td key={role.name} className="role">
              {role.permissions.includes(permission) ? (
                <span className="dot" aria-label={t("access.roles.holds", { role: role.name, permission })} />
              ) : (
                <span className="dash">–</span>
              )}
            </td>
          ))}
        </tr>
      ))}
    </>
  );
}

// A count drawn bold inside its plural phrase, "<b>3</b> people".
function counted(phrase: string, count: number) {
  return <>{withValues(phrase, { n: <b>{count}</b> })}</>;
}
