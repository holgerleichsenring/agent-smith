import type { AccessDocument, AccessGroup, AccessPerson, AccessView } from "@/lib/accessApi";

// 2026-08-26-7a51: the panes as the DRAFT would leave them.
//
// The server composes the same four views from the saved document; while an administrator
// is editing, the rows have to reflect what they have typed rather than what is stored, so
// the same composition runs here over the draft. Both sides derive from one document,
// which is why they cannot disagree about what a save will mean.
//
// A grant matches on {claim, value} and compares the value ORDINALLY, exactly as the
// server does — `Ada@example.com` and `ada@example.com` are two identifiers.

/** The roles the draft grants to one name-claim value. */
export function grantedTo(draft: AccessDocument, claim: string, value: string): string[] {
  return draft.personGrants
    .filter((grant) => grant.claim === claim && grant.value === value)
    .flatMap((grant) => grant.roles);
}

/** Everyone the surface shows: observed callers, plus grants naming nobody seen yet. */
export function peopleOf(view: AccessView, draft: AccessDocument): AccessPerson[] {
  const seen = view.people.map((person) => ({
    ...person,
    grantedRoles: grantedTo(draft, view.nameClaim, person.nameValue),
  }));
  const named = new Set(seen.map((person) => person.nameValue));
  const added = draft.personGrants
    .filter((grant) => !named.has(grant.value))
    .map((grant) => addedByHand(grant.claim, grant.value, grant.roles));
  // Never-seen first, exactly as the server composes them: they are the rows somebody just
  // typed, and a row that scrolled below the fold the moment it was added is a row nobody
  // can tell was added at all.
  return [...added, ...seen];
}

/** Every group value: carried by somebody, mapped by the draft, or both. */
export function groupsOf(view: AccessView, draft: AccessDocument): AccessGroup[] {
  const carried = new Map(view.groups.map((group) => [group.value, group.carriers]));
  const values = new Set([...carried.keys(), ...Object.keys(draft.groupRoles).map(unslashed)]);
  return [...values].sort().map((value) => ({
    value,
    roles: draft.groupRoles[value] ?? draft.groupRoles[`/${value}`] ?? [],
    carriers: carried.get(value) ?? 0,
  }));
}

/** How many of the surface's people hold this role, from either direction. */
export function peopleHolding(people: AccessPerson[], role: string): number {
  return people.filter(
    (person) =>
      person.grantedRoles.some((held) => same(held, role))
      || person.directoryRoles.some((origin) => same(origin.role, role)),
  ).length;
}

export function groupsGranting(groups: AccessGroup[], role: string): number {
  return groups.filter((group) => group.roles.some((held) => same(held, role))).length;
}

/** The permission catalog grouped by the area its names already state. */
export function permissionAreas(permissions: string[]): [string, string[]][] {
  const areas = new Map<string, string[]>();
  for (const permission of permissions) {
    const area = permission.split(".")[0];
    areas.set(area, [...(areas.get(area) ?? []), permission]);
  }
  return [...areas.entries()];
}

// A Keycloak group path is "/platform-admins"; the value an operator copies out of the
// console is "platform-admins". One leading slash, and only that.
function unslashed(value: string): string {
  return value.startsWith("/") ? value.slice(1) : value;
}

// Role NAMES fold case (a directory decides the capitalisation and an operator cannot);
// the identifiers a grant is written against do not.
function same(left: string, right: string): boolean {
  return left.toLowerCase() === right.toLowerCase();
}

function addedByHand(claim: string, value: string, roles: string[]): AccessPerson {
  return {
    id: value,
    subject: null,
    nameClaim: claim,
    nameValue: value,
    directoryRoles: [],
    grantedRoles: roles,
    groupValues: [],
    groupsOmitted: false,
    firstSeen: null,
    lastSeen: null,
  };
}
