// 2026-08-26-7a51: the access surface's client — who may do what, in four panes over one
// document.
//
// GET /api/access answers the panes AND the document they were derived from. The document
// is what a save sends back, because the panes are derived: a custom role's bundle reaches
// them with the permissions the server's catalog does not know already dropped, so a
// surface that rebuilt the document from what it renders would rewrite the very roles this
// installation is promised to keep.
//
// A save is a WHOLE-document PUT for the same reason a settings save is: the server binds
// the body onto a fresh model, so every field omitted reverts to its default — a
// people-only body would reset the claim names and delete the custom roles.

import { apiFetch, getJson, readJson, sendJson } from "@/lib/apiResponse";

/** One role a caller holds because the DIRECTORY says so, and the claim it arrived through. */
export interface AccessRoleOrigin {
  role: string;
  via: string;
}

/** A grant stored against the claim it was written for — never a bare value. */
export interface PersonGrant {
  claim: string;
  value: string;
  roles: string[];
}

/** 2026-10-15-4a4df: one person in a project binding, against the claim it was written for. */
export interface PersonRef {
  claim: string;
  value: string;
}

/** 2026-10-15-4a4df: roles held in ONE project by directory groups and named people. */
export interface ProjectBinding {
  project: string;
  groups: string[];
  people: PersonRef[];
  roles: string[];
}

/** The stored role mapping, exactly as the server holds it. What a save sends back. */
export interface AccessDocument {
  roleClaim: string;
  groupClaim: string;
  groupRoles: Record<string, string[]>;
  roles: Record<string, string[]>;
  personGrants: PersonGrant[];
  projectBindings: ProjectBinding[];
  observationRetentionDays: number;
}

export interface AccessPerson {
  /** What removes this person — the subject when seen, the granted value otherwise. */
  id: string;
  subject: string | null;
  nameClaim: string;
  nameValue: string;
  directoryRoles: AccessRoleOrigin[];
  grantedRoles: string[];
  groupValues: string[];
  groupsOmitted: boolean;
  firstSeen: string | null;
  /** Absent for somebody added by hand who has not called yet. */
  lastSeen: string | null;
}

export interface AccessGroup {
  value: string;
  roles: string[];
  carriers: number;
}

export interface AccessRole {
  name: string;
  builtIn: boolean;
  permissions: string[];
  people: number;
  groups: number;
}

export interface AccessView {
  roleClaim: string;
  groupClaim: string;
  nameClaim: string;
  document: AccessDocument;
  /** The name claim is not `sub`, so its values may be editable by the people they name. */
  nameClaimIsSelfAsserted: boolean;
  observationRetentionDays: number;
  people: AccessPerson[];
  groups: AccessGroup[];
  roles: AccessRole[];
  permissions: string[];
  findings: string[];
  /** 2026-10-15-4a4df: the configured project names, sorted as the server compares them. */
  projects: string[];
  /** 2026-10-15-4a4df: the permissions a project binding can grant; the rest stay global. */
  projectScopable: string[];
}

const ROUTE = "/api/access";

export async function fetchAccess(signal?: AbortSignal): Promise<AccessView> {
  return getJson<AccessView>(ROUTE, signal);
}

export async function saveAccess(
  document: AccessDocument,
  signal?: AbortSignal,
): Promise<AccessView> {
  return sendJson<AccessView>("PUT", ROUTE, document, signal);
}

/** One action: the person's grant and the record of having seen them, together. */
export async function forgetPerson(id: string, signal?: AbortSignal): Promise<AccessView> {
  const path = `${ROUTE}/people/${encodeURIComponent(id)}`;
  return readJson<AccessView>(await apiFetch(path, { method: "DELETE", signal }), path);
}
