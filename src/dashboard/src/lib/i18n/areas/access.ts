import type { MessageEntry } from "../messages";

// 2026-11-23-5669a: the access area's part of the one dictionary, spread into messages; English and German side by side.
// 2026-11-23-5669e: Access (who may do what) under access.*, and the Your identity page under identity.*.

export const accessMessages = {
  // --- Access: the studio ---
  "access.surface": { en: "the access settings", de: "die Zugriffseinstellungen" },
  "access.loading": { en: "Loading…", de: "Wird geladen…" },
  "access.title": { en: "Who may do what", de: "Wer was darf" },
  "access.sub": {
    en: "people, groups, roles and the claims they are read from",
    de: "Personen, Gruppen, Rollen und die Claims, aus denen sie gelesen werden",
  },
  "access.saving": { en: "Saving…", de: "Wird gespeichert…" },
  "access.save": { en: "Save changes", de: "Änderungen speichern" },
  "access.legend.group": { en: "group", de: "Gruppe" },
  "access.legend.fromDirectory": { en: "from your directory", de: "aus deinem Verzeichnis" },
  "access.legend.grantedHere": { en: "granted here", de: "hier vergeben" },
  "access.nameClaimWarning": {
    en: "Callers are named by {claim}, not {sub}. A value like that is self-asserted in common directory configurations — somebody who can change their own can claim a grant written for someone else.",
    de: "Aufrufer werden über {claim} benannt, nicht über {sub}. So ein Wert ist in üblichen Verzeichnis-Konfigurationen selbst angegeben – wer den eigenen ändern kann, kann eine Vergabe für jemand anderen beanspruchen.",
  },
  "access.tabs": { en: "Access settings", de: "Zugriffseinstellungen" },
  "access.tab.people": { en: "People", de: "Personen" },
  "access.tab.groups": { en: "Groups", de: "Gruppen" },
  "access.tab.roles": { en: "Roles", de: "Rollen" },
  "access.tab.projects": { en: "Projects", de: "Projekte" },
  "access.tab.claims": { en: "Claim names", de: "Claim-Namen" },

  // --- Access: the paged table ---
  "access.paged.showing": { en: "Showing {shown} of {total}", de: "{shown} von {total} angezeigt" },
  "access.paged.more": { en: "Show {count} more", de: "{count} weitere anzeigen" },
  "access.filter.all": { en: "All", de: "Alle" },

  // --- Access: role chips and member chips ---
  "access.chips.granted": { en: "granted", de: "vergeben" },
  "access.chips.withdraw": { en: "Withdraw {role}", de: "{role} entziehen" },
  "access.chips.noRole": { en: "no role", de: "keine Rolle" },
  "access.chips.grant": { en: "+ grant", de: "+ vergeben" },
  "access.members.remove": { en: "Remove {member}", de: "{member} entfernen" },
  "access.members.addTo": { en: "Add to {label}", de: "Zu {label} hinzufügen" },
  "access.members.pick": { en: "+ pick", de: "+ auswählen" },
  "access.members.typeFor": { en: "Type a value for {label}", de: "Wert für {label} eingeben" },
  "access.members.add": { en: "Add", de: "Hinzufügen" },

  // --- Access → People ---
  "access.people.search": { en: "Search people", de: "Personen suchen" },
  "access.people.searchPlaceholder": { en: "Search by name or subject", de: "Nach Name oder Subject suchen" },
  "access.people.grantedHere": { en: "Granted here", de: "Hier vergeben" },
  "access.people.noRole": { en: "No role", de: "Keine Rolle" },
  "access.people.empty": { en: "Nobody matches that.", de: "Darauf passt niemand." },
  "access.people.person": { en: "Person", de: "Person" },
  "access.people.lastSeen": { en: "Last seen", de: "Zuletzt gesehen" },
  "access.people.roles": { en: "Roles", de: "Rollen" },
  "access.people.subject": { en: "Subject", de: "Subject" },
  "access.people.valuePlaceholder": { en: "{claim} value your directory sends", de: "{claim}-Wert, den dein Verzeichnis sendet" },
  "access.people.add": { en: "Add person", de: "Person hinzufügen" },
  "access.people.sub": { en: "sub · {subject}", de: "sub · {subject}" },
  "access.people.notSignedIn": { en: "not signed in yet", de: "noch nicht angemeldet" },
  "access.people.grantTo": { en: "Grant a role to {name}", de: "{name} eine Rolle vergeben" },
  "access.people.forget": { en: "forget", de: "vergessen" },

  // --- Access → Groups ---
  "access.groups.search": { en: "Search groups", de: "Gruppen suchen" },
  "access.groups.searchPlaceholder": { en: "Search by group value", de: "Nach Gruppenwert suchen" },
  "access.groups.mapped": { en: "Mapped", de: "Zugeordnet" },
  "access.groups.unmapped": { en: "Unmapped", de: "Nicht zugeordnet" },
  "access.groups.empty": { en: "No group matches that.", de: "Darauf passt keine Gruppe." },
  "access.groups.value": { en: "Group value", de: "Gruppenwert" },
  "access.groups.carriedBy": { en: "Carried by", de: "Getragen von" },
  "access.groups.grants": { en: "Grants", de: "Vergibt" },
  "access.groups.valuePlaceholder": { en: "group value your directory sends", de: "Gruppenwert, den dein Verzeichnis sendet" },
  "access.groups.add": { en: "Add group", de: "Gruppe hinzufügen" },
  "access.groups.people": {
    en: { one: "1 person", other: "{count} people" },
    de: { one: "1 Person", other: "{count} Personen" },
  },
  "access.groups.mapOnto": { en: "Map {group} onto a role", de: "{group} einer Rolle zuordnen" },

  // --- Access → Roles ---
  "access.roles.permissionsOf": { en: "{held} of {total} permissions", de: "{held} von {total} Berechtigungen" },
  "access.roles.carriedBy": { en: "carried by {people} · {groups}", de: "getragen von {people} · {groups}" },
  // {n} is drawn bold by the pane, so the count picks the form and {n} is filled there.
  "access.roles.people": {
    en: { one: "{n} person", other: "{n} people" },
    de: { one: "{n} Person", other: "{n} Personen" },
  },
  "access.roles.groups": {
    en: { one: "{n} group", other: "{n} groups" },
    de: { one: "{n} Gruppe", other: "{n} Gruppen" },
  },
  "access.roles.custom": { en: "configured here · read-only", de: "hier konfiguriert · schreibgeschützt" },
  "access.roles.permission": { en: "Permission", de: "Berechtigung" },
  "access.roles.holds": { en: "{role} holds {permission}", de: "{role} hat {permission}" },
  "access.roles.configuredHere": { en: "Roles configured here", de: "Hier konfigurierte Rollen" },
  "access.roles.help": {
    en: "A role composed here stands beside the built-in three and never replaces one. Removing it is refused while anybody this installation can see still holds it.",
    de: "Eine hier zusammengestellte Rolle steht neben den drei eingebauten und ersetzt nie eine davon. Entfernen wird abgelehnt, solange jemand, den diese Installation sieht, sie noch hat.",
  },
  "access.roles.remove": { en: "Remove", de: "Entfernen" },
  "access.roles.newName": { en: "new role name", de: "Name der neuen Rolle" },
  "access.roles.add": { en: "Add role", de: "Rolle hinzufügen" },

  // --- Access → Projects ---
  "access.projects.search": { en: "Search projects", de: "Projekte suchen" },
  "access.projects.searchPlaceholder": { en: "Search by project name", de: "Nach Projektname suchen" },
  "access.projects.bound": { en: "Bound", de: "Gebunden" },
  "access.projects.unbound": { en: "Unbound", de: "Ungebunden" },
  "access.projects.notConfiguredFilter": { en: "Not configured", de: "Nicht konfiguriert" },
  "access.projects.empty": { en: "No project matches that.", de: "Darauf passt kein Projekt." },
  "access.projects.project": { en: "Project", de: "Projekt" },
  "access.projects.bindings": {
    en: "Bindings · roles held in this project only",
    de: "Bindungen · Rollen nur in diesem Projekt",
  },
  "access.projects.notConfigured": { en: "not configured", de: "nicht konfiguriert" },
  "access.projects.addBinding": { en: "Add binding", de: "Bindung hinzufügen" },
  "access.binding.title": { en: "Binding", de: "Bindung" },
  "access.binding.remove": { en: "Remove", de: "Entfernen" },
  "access.binding.groups": { en: "groups · {claim}", de: "Gruppen · {claim}" },
  "access.binding.people": { en: "people · {claim}", de: "Personen · {claim}" },
  "access.binding.groupPlaceholder": { en: "group value your directory sends", de: "Gruppenwert, den dein Verzeichnis sendet" },
  "access.binding.grantIn": { en: "Grant a role in {project}", de: "Eine Rolle in {project} vergeben" },
  "access.binding.grants": { en: "grants here: {permissions}", de: "vergibt hier: {permissions}" },
  "access.binding.grantsNothing": { en: "grants nothing here yet", de: "vergibt hier noch nichts" },
  "access.binding.ignored": { en: "ignored here: {permissions}", de: "hier ignoriert: {permissions}" },

  // --- Access → Claim names ---
  "access.claims.role": { en: "Role claim", de: "Rollen-Claim" },
  "access.claims.roleHelp": {
    en: "the claim role NAMES are read out of, verbatim",
    de: "der Claim, aus dem Rollen-NAMEN wörtlich gelesen werden",
  },
  "access.claims.group": { en: "Group claim", de: "Gruppen-Claim" },
  "access.claims.groupHelp": { en: "the claim group values are read out of", de: "der Claim, aus dem Gruppenwerte gelesen werden" },
  "access.claims.name": { en: "Name claim", de: "Namens-Claim" },
  "access.claims.nameHelp": {
    en: "bootstrap — changing it costs a restart",
    de: "Bootstrap – eine Änderung kostet einen Neustart",
  },
  "access.claims.retention": { en: "Observation retention (days)", de: "Aufbewahrung der Beobachtungen (Tage)" },
  "access.claims.retentionHelp": {
    en: "how long a caller stays on the People pane after their last request",
    de: "wie lange ein Aufrufer nach seiner letzten Anfrage in der Personen-Ansicht bleibt",
  },

  // --- Your identity ---
  "identity.title": { en: "Your identity", de: "Deine Identität" },
  "identity.sub": {
    en: "What your token carried, and what this installation made of it.",
    de: "Was dein Token enthielt und was diese Installation daraus gemacht hat.",
  },
  "identity.surface": { en: "your identity", de: "deine Identität" },
  "identity.failedSurface": { en: "identity", de: "Identität" },
  "identity.reading": { en: "Reading your identity…", de: "Deine Identität wird gelesen…" },
  "identity.nothingRead": { en: "Nothing has been read yet.", de: "Bisher wurde nichts gelesen." },
  "identity.unconfigured": {
    en: "This dashboard has no authority configured, so nothing signs in and every call it makes is anonymous.",
    de: "Für dieses Dashboard ist keine Authority konfiguriert, also meldet sich niemand an und jeder Aufruf ist anonym.",
  },

  // --- Your identity: the claims ---
  "identity.signedInAs": { en: "Signed in as", de: "Angemeldet als" },
  "identity.subject": { en: "Subject", de: "Subject" },
  "identity.noSubject": { en: "— the token named no subject", de: "– das Token nannte kein Subject" },
  "identity.issuer": { en: "Issuer", de: "Issuer" },
  "identity.noIssuer": { en: "— the token named no issuer", de: "– das Token nannte keinen Issuer" },
  "identity.whatArrived": { en: "What arrived", de: "Was ankam" },
  "identity.roleClaim": { en: "Role claim · {claim}", de: "Rollen-Claim · {claim}" },
  "identity.groupClaim": { en: "Group claim · {claim}", de: "Gruppen-Claim · {claim}" },
  "identity.resolvedTo": { en: "What it resolved to", de: "Was daraus wurde" },
  "identity.roles": { en: "Roles", de: "Rollen" },
  "identity.permissions": { en: "Permissions", de: "Berechtigungen" },
  "identity.noticed": { en: "What the server noticed", de: "Was dem Server auffiel" },
  "identity.perProject": { en: "Per project", de: "Pro Projekt" },
  "identity.noRoles": {
    en: "Your token carried no role this installation maps. The values above are what a mapping is written from — it is server configuration, and your directory decides which of them you carry.",
    de: "Dein Token enthielt keine Rolle, die diese Installation zuordnet. Aus den Werten oben wird eine Zuordnung geschrieben – das ist Server-Konfiguration, und dein Verzeichnis entscheidet, welche davon du trägst.",
  },
  "identity.nothingArrived": { en: "nothing arrived", de: "nichts angekommen" },

  // --- Your identity: a scope naming another resource ---
  "identity.scope.title": { en: "These scopes name a different resource", de: "Diese Scopes nennen eine andere Ressource" },
  "identity.scope.body": {
    en: "This dashboard asks for a token for {asked}, and this server validates tokens for {validated}. A token minted for the first will be refused by the second.",
    de: "Dieses Dashboard fordert ein Token für {asked} an, dieser Server prüft aber Tokens für {validated}. Ein Token für das eine wird vom anderen abgelehnt.",
  },
  "identity.scope.remedy": {
    en: "Point the dashboard's scopes at this server's API. Silence here is not a promise that sign-in will work — a scope nobody has consented to, and an API minting a different token version, both pass this comparison.",
    de: "Richte die Scopes des Dashboards auf die API dieses Servers. Schweigt dieser Hinweis, heißt das nicht, dass die Anmeldung klappt – ein Scope ohne Zustimmung und eine API mit anderer Token-Version bestehen diesen Vergleich beide.",
  },

  // --- Your identity: a refused token ---
  "identity.refused.title": { en: "This server did not accept your token", de: "Dieser Server hat dein Token nicht akzeptiert" },
  "identity.refused.noMapping": {
    en: "No role mapping can change this: a mapping decides what an ACCEPTED token grants, and this one was refused before any of it was read.",
    de: "Keine Rollenzuordnung ändert daran etwas: eine Zuordnung bestimmt, was ein AKZEPTIERTES Token gewährt, und dieses wurde abgelehnt, bevor etwas davon gelesen wurde.",
  },
  "identity.refused.audienceExpected": { en: "Audience this server expects", de: "Audience, die dieser Server erwartet" },
  "identity.refused.audienceCarried": { en: "Audience your token carried", de: "Audience in deinem Token" },
  "identity.refused.authority": {
    en: "Authority this server validates against",
    de: "Authority, gegen die dieser Server prüft",
  },
  "identity.refused.issuer": { en: "Issuer your token names", de: "Issuer, den dein Token nennt" },
  "identity.refused.version": { en: "Token version your token declares", de: "Token-Version, die dein Token angibt" },
  "identity.refused.notReadable": { en: "— not readable from what arrived", de: "– aus dem Angekommenen nicht lesbar" },
  "identity.refused.noneConfigured": { en: "— none configured", de: "– nichts konfiguriert" },
  "identity.reason.expired": {
    en: "Your token has expired. Sign out and in again; if it happens immediately every time, the token's lifetime is shorter than the clock skew between the two machines.",
    de: "Dein Token ist abgelaufen. Meld dich ab und wieder an; passiert das jedes Mal sofort, ist die Lebensdauer des Tokens kürzer als die Zeitabweichung zwischen den beiden Maschinen.",
  },
  "identity.reason.notYetValid": {
    en: "Your token is not valid yet, which means this server's clock and your identity provider's disagree. Check the time on both.",
    de: "Dein Token ist noch nicht gültig – die Uhr dieses Servers und die deines Identity-Providers gehen also unterschiedlich. Prüf die Zeit auf beiden.",
  },
  "identity.reason.audience": {
    en: "Your token was not issued for the audience this server accepts. The dashboard is asking your authority for a token for a different audience than the server was configured with.",
    de: "Dein Token wurde nicht für die Audience ausgestellt, die dieser Server akzeptiert. Das Dashboard fordert bei deiner Authority ein Token für eine andere Audience an, als der Server konfiguriert hat.",
  },
  "identity.reason.issuer": {
    en: "Your token was issued by a different authority than the one this server validates against. One of the two authorities is wrong — they are shown below.",
    de: "Dein Token stammt von einer anderen Authority als der, gegen die dieser Server prüft. Eine der beiden Authorities ist falsch – beide stehen unten.",
  },
  "identity.reason.signature": {
    en: "Your token's signature did not verify against the keys this authority publishes. The authority's signing keys have rotated, or the token did not come from it.",
    de: "Die Signatur deines Tokens ließ sich mit den Schlüsseln dieser Authority nicht prüfen. Entweder wurden ihre Signaturschlüssel rotiert, oder das Token stammt nicht von ihr.",
  },
  "identity.reason.malformed": {
    en: "What arrived was not a well-formed token, so nothing in it could be read.",
    de: "Was ankam, war kein gültig aufgebautes Token, also ließ sich nichts darin lesen.",
  },
  "identity.reason.rejected": {
    en: "Your token was refused, and the check that refused it is not one this server names separately. The server's log carries the detail.",
    de: "Dein Token wurde abgelehnt, und die Prüfung, die es ablehnte, benennt dieser Server nicht einzeln. Die Details stehen im Server-Log.",
  },

  // --- Your identity: a recognised token shape ---
  "identity.shape.signInClient": {
    en: "This token was issued for the sign-in client rather than for this API, so it was never addressed to the server that just refused it.",
    de: "Dieses Token wurde für den Anmelde-Client ausgestellt statt für diese API, war also nie an den Server gerichtet, der es gerade abgelehnt hat.",
  },
  "identity.shape.signInClientWay": {
    en: "Add this API's scope to the dashboard's configured scopes — without it the authority mints a token for the client and the server refuses every call.",
    de: "Nimm den Scope dieser API in die konfigurierten Scopes des Dashboards auf – ohne ihn stellt die Authority ein Token für den Client aus, und der Server lehnt jeden Aufruf ab.",
  },
  "identity.shape.v1": {
    en: "This is a version 1 access token: its audience is the API's identifier URI and its issuer is the directory's v1 endpoint, while this server is configured for the version 2 pair. The resource decides the version, never the endpoint the sign-in used — in Microsoft Entra that is requestedAccessTokenVersion on the API's own app registration, which mints version 1 while it is unset.",
    de: "Das ist ein Access-Token der Version 1: seine Audience ist die Identifier-URI der API und sein Issuer der v1-Endpunkt des Verzeichnisses, während dieser Server für das Paar der Version 2 konfiguriert ist. Die Version bestimmt die Ressource, nie der Endpunkt der Anmeldung – in Microsoft Entra ist das requestedAccessTokenVersion in der App-Registrierung der API, die Version 1 ausstellt, solange der Wert nicht gesetzt ist.",
  },
  "identity.shape.v1WayVersion": {
    en: "Set requestedAccessTokenVersion to 2 on the API registration, after which the audience arrives as {expected}.",
    de: "Setz requestedAccessTokenVersion in der API-Registrierung auf 2, danach kommt die Audience als {expected} an.",
  },
  "identity.shape.v1WayServer": {
    en: "Or configure this server for the version 1 pair: the audience {presented} and the authority without its /v2.0 suffix, whose discovery document names the v1 issuer.",
    de: "Oder konfigurier diesen Server für das Paar der Version 1: die Audience {presented} und die Authority ohne ihr /v2.0-Suffix, deren Discovery-Dokument den v1-Issuer nennt.",
  },
  "identity.shape.v2": {
    en: "This is a version 2 access token, whose audience is the API's bare client id, while this server is configured for the version 1 pair.",
    de: "Das ist ein Access-Token der Version 2, dessen Audience die reine Client-ID der API ist, während dieser Server für das Paar der Version 1 konfiguriert ist.",
  },
  "identity.shape.v2WayServer": {
    en: "Configure this server with the audience {presented} and the authority carrying its /v2.0 suffix.",
    de: "Konfigurier diesen Server mit der Audience {presented} und der Authority mit ihrem /v2.0-Suffix.",
  },
  "identity.shape.v2WayVersion": {
    en: "Or set the API registration's accepted access-token version back to 1, which mints the identifier-URI form again.",
    de: "Oder setz die akzeptierte Access-Token-Version der API-Registrierung wieder auf 1, dann wird wieder die Identifier-URI-Form ausgestellt.",
  },
} satisfies Record<string, MessageEntry>;
