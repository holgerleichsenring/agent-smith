"use client";

import type { AccessView, PersonRef, ProjectBinding } from "@/lib/accessApi";
import { MemberChips } from "./MemberChips";
import { RoleChips } from "./RoleChips";
import { effectivePermissions, projectRoles } from "./projectBindings";
import { useT } from "@/hooks/useT";

// 2026-10-15-4a4df: one project binding — a member set times a role set. A person is written
// against the claim callers are named by today, as the People pane writes a grant; one written
// against another claim keeps its claim in the chip, because it grants nothing until rewritten.

export function BindingCard({
  testId,
  view,
  binding,
  people,
  onGroups,
  onPeople,
  onRoles,
  onRemove,
}: {
  testId: string;
  view: AccessView;
  binding: ProjectBinding;
  people: string[];
  onGroups: (groups: string[]) => void;
  onPeople: (people: PersonRef[]) => void;
  onRoles: (roles: string[]) => void;
  onRemove: () => void;
}) {
  const t = useT();
  const shown = (ref: PersonRef) => (ref.claim === view.nameClaim ? ref.value : `${ref.claim}:${ref.value}`);
  const { scoped, ignored } = effectivePermissions(view, binding.roles);

  return (
    <div className="binding" data-testid={testId}>
      <div className="binding-head">
        <span className="binding-title">{t("access.binding.title")}</span>
        <button type="button" className="forget" data-testid={`${testId}-remove`} onClick={onRemove}>
          {t("access.binding.remove")}
        </button>
      </div>
      <MemberChips
        testId={`${testId}-groups`}
        label={t("access.binding.groups", { claim: view.groupClaim })}
        members={binding.groups}
        offered={view.groups.map((group) => group.value)}
        placeholder={t("access.binding.groupPlaceholder")}
        onAdd={(value) => onGroups([...binding.groups, value])}
        onRemove={(value) => onGroups(binding.groups.filter((group) => group !== value))}
      />
      <MemberChips
        testId={`${testId}-people`}
        label={t("access.binding.people", { claim: view.nameClaim })}
        members={binding.people.map(shown)}
        offered={people}
        placeholder={t("access.people.valuePlaceholder", { claim: view.nameClaim })}
        onAdd={(value) => onPeople([...binding.people, { claim: view.nameClaim, value }])}
        onRemove={(value) => onPeople(binding.people.filter((ref) => shown(ref) !== value))}
      />
      <RoleChips
        testId={`${testId}-roles`}
        directory={[]}
        granted={binding.roles}
        offered={projectRoles(view)}
        grantLabel={t("access.binding.grantIn", { project: binding.project })}
        onGrant={(role) => onRoles([...binding.roles, role])}
        onWithdraw={(role) => onRoles(binding.roles.filter((held) => held !== role))}
      />
      <p className="binding-effect" data-testid={`${testId}-effective`}>
        {scoped.length > 0
          ? t("access.binding.grants", { permissions: scoped.join(", ") })
          : t("access.binding.grantsNothing")}
      </p>
      {ignored.length > 0 && (
        <p className="binding-effect ignored" data-testid={`${testId}-ignored`}>
          {t("access.binding.ignored", { permissions: ignored.join(", ") })}
        </p>
      )}
    </div>
  );
}
