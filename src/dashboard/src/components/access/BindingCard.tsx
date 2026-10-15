"use client";

import type { AccessView, PersonRef, ProjectBinding } from "@/lib/accessApi";
import { MemberChips } from "./MemberChips";
import { RoleChips } from "./RoleChips";
import { effectivePermissions, projectRoles } from "./projectBindings";

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
  const shown = (ref: PersonRef) => (ref.claim === view.nameClaim ? ref.value : `${ref.claim}:${ref.value}`);
  const { scoped, ignored } = effectivePermissions(view, binding.roles);

  return (
    <div className="binding" data-testid={testId}>
      <div className="binding-head">
        <span className="binding-title">Binding</span>
        <button type="button" className="forget" data-testid={`${testId}-remove`} onClick={onRemove}>
          Remove
        </button>
      </div>
      <MemberChips
        testId={`${testId}-groups`}
        label={`groups · ${view.groupClaim}`}
        members={binding.groups}
        offered={view.groups.map((group) => group.value)}
        placeholder="group value your directory sends"
        onAdd={(value) => onGroups([...binding.groups, value])}
        onRemove={(value) => onGroups(binding.groups.filter((group) => group !== value))}
      />
      <MemberChips
        testId={`${testId}-people`}
        label={`people · ${view.nameClaim}`}
        members={binding.people.map(shown)}
        offered={people}
        placeholder={`${view.nameClaim} value your directory sends`}
        onAdd={(value) => onPeople([...binding.people, { claim: view.nameClaim, value }])}
        onRemove={(value) => onPeople(binding.people.filter((ref) => shown(ref) !== value))}
      />
      <RoleChips
        testId={`${testId}-roles`}
        directory={[]}
        granted={binding.roles}
        offered={projectRoles(view)}
        grantLabel={`Grant a role in ${binding.project}`}
        onGrant={(role) => onRoles([...binding.roles, role])}
        onWithdraw={(role) => onRoles(binding.roles.filter((held) => held !== role))}
      />
      <p className="binding-effect" data-testid={`${testId}-effective`}>
        {scoped.length > 0 ? `grants here: ${scoped.join(", ")}` : "grants nothing here yet"}
      </p>
      {ignored.length > 0 && (
        <p className="binding-effect ignored" data-testid={`${testId}-ignored`}>
          ignored here: {ignored.join(", ")}
        </p>
      )}
    </div>
  );
}
