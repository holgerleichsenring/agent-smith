import type { MessageEntry } from "../messages";

// 2026-11-23-5669a: the shell's part of the one dictionary — sidebar, top bar, banners, the failure and refusal panels,
// home, the overview, the event drawer and the pull-requests page. English and German side by side.

export const shellMessages = {
  // --- the sidebar ---
  "shell.nav.main": { en: "Main", de: "Hauptnavigation" },
  "shell.nav.home": { en: "Home", de: "Startseite" },
  "shell.nav.expand": { en: "Expand the sidebar", de: "Seitenleiste aufklappen" },
  "shell.nav.collapse": { en: "Collapse the sidebar", de: "Seitenleiste einklappen" },
  "shell.nav.newWork": { en: "New work", de: "Neue Arbeit" },
  "shell.nav.workItOut": { en: "Work it out", de: "Work it out" },
  "shell.nav.yourMove": { en: "Your move", de: "Du bist dran" },
  "shell.nav.tickets": { en: "Tickets", de: "Tickets" },
  "shell.nav.runs": { en: "Runs", de: "Runs" },
  "shell.nav.pullRequests": { en: "Pull requests", de: "Pull Requests" },
  "shell.nav.insight": { en: "Insight", de: "Auswertung" },
  "shell.nav.overview": { en: "Overview", de: "Übersicht" },
  "shell.nav.delivery": { en: "Delivery", de: "Delivery" },
  "shell.nav.backToWork": { en: "Back to work", de: "Zurück zur Arbeit" },
  "shell.nav.dot.attention": { en: "needs attention", de: "braucht Aufmerksamkeit" },
  "shell.nav.dot.live": { en: "live", de: "live" },
  "shell.nav.dot.idle": { en: "idle", de: "inaktiv" },
  "shell.nav.recent": { en: "Recent", de: "Zuletzt geöffnet" },

  // --- the top bar ---
  "shell.search.placeholder": { en: "Go to a page", de: "Zu einer Seite springen" },
  "shell.bell.waiting": { en: "{count} waiting on you", de: "{count} warten auf dich" },
  "shell.bell.nothing": { en: "Nothing waits on you", de: "Nichts wartet auf dich" },
  "shell.place.home": { en: "Home", de: "Startseite" },
  "shell.place.conversations": { en: "Conversations", de: "Unterhaltungen" },
  "shell.place.identity": { en: "Your identity", de: "Deine Identität" },
  "shell.place.system": { en: "System", de: "System" },
  "shell.place.chat": { en: "Chat dispatchers", de: "Chat-Dispatcher" },
  "shell.place.configReads": { en: "Config file reads", de: "Gelesene Konfigurationsdateien" },
  "shell.place.catalog": { en: "Skill catalog & vocabulary", de: "Skill-Katalog & Vokabular" },
  "shell.place.attention": { en: "Needs attention", de: "Braucht Aufmerksamkeit" },
  "shell.place.permissions": { en: "Permissions", de: "Berechtigungen" },
  "shell.place.changes": { en: "Changes", de: "Änderungen" },
  "shell.place.installation": { en: "Installation", de: "Installation" },
  "shell.place.connectionCheck": { en: "Connection check", de: "Verbindungstest" },
  "shell.runsPage.sub": {
    en: "Tickets picked up from your trackers, worked as jobs.",
    de: "Tickets aus deinen Trackern, als Jobs abgearbeitet.",
  },

  // --- banners ---
  "shell.degraded.title": {
    en: {
      one: "Running degraded — 1 blocking finding. Everything not named below still runs.",
      other: "Running degraded — {count} blocking findings. Everything not named below still runs.",
    },
    de: {
      one: "Eingeschränkter Betrieb – 1 blockierender Befund. Alles, was unten nicht genannt ist, läuft weiter.",
      other: "Eingeschränkter Betrieb – {count} blockierende Befunde. Alles, was unten nicht genannt ist, läuft weiter.",
    },
  },
  "shell.build.reload": { en: "Reload", de: "Neu laden" },
  "shell.auth.oneSide": {
    en: "Sign-in is configured on one side only.",
    de: "Die Anmeldung ist nur auf einer Seite eingerichtet.",
  },
  "shell.auth.twoAuthorities": {
    en: "The two halves of sign-in name different authorities.",
    de: "Die beiden Hälften der Anmeldung nennen verschiedene Authorities.",
  },
  "shell.auth.both": {
    en: "This dashboard signs in against {dashboard}, and the server validates tokens from {server}. A token minted by one is refused by the other; one of the two is wrong.",
    de: "Dieses Dashboard meldet sich bei {dashboard} an, der Server prüft Tokens von {server}. Ein Token der einen Seite wird von der anderen abgelehnt – eine der beiden Angaben ist falsch.",
  },
  "shell.auth.anAuthority": { en: "an authority", de: "einer Authority" },
  "shell.auth.dashboardEnforced": {
    en: "The server enforces sign-in against {authority} and this dashboard has no authority configured, so every call it makes is refused. Set the authority in the dashboard's runtime settings.",
    de: "Der Server verlangt eine Anmeldung bei {authority}, aber dieses Dashboard hat keine Authority eingerichtet, also wird jeder seiner Aufrufe abgelehnt. Trag die Authority in den Laufzeit-Einstellungen des Dashboards ein.",
  },
  "shell.auth.dashboardNotEnforced": {
    en: "The server validates tokens from {authority} and this dashboard has no authority configured, so nobody can sign in. Nothing is refused yet — enforcement is off.",
    de: "Der Server prüft Tokens von {authority}, aber dieses Dashboard hat keine Authority eingerichtet, also kann sich niemand anmelden. Abgelehnt wird noch nichts – die Durchsetzung ist aus.",
  },
  "shell.auth.server": {
    en: "This dashboard signs in against {dashboard} and the server has no authority configured, so it validates no token and reads every caller as anonymous. Set the authority in the server's auth configuration.",
    de: "Dieses Dashboard meldet sich bei {dashboard} an, aber der Server hat keine Authority eingerichtet. Er prüft also kein Token und behandelt jeden Aufruf als anonym. Trag die Authority in der Auth-Konfiguration des Servers ein.",
  },

  // --- sign-in ---
  "shell.signIn.expired": { en: "Your session expired.", de: "Deine Sitzung ist abgelaufen." },
  "shell.signIn.ended": { en: "Your session ended.", de: "Deine Sitzung wurde beendet." },
  "shell.signIn.leaving": {
    en: "Taking you to your organisation's sign-in page.",
    de: "Du wirst zur Anmeldeseite deiner Organisation weitergeleitet.",
  },
  "shell.signIn.asks": {
    en: "This installation asks for a sign-in before it shows anything.",
    de: "Diese Installation zeigt erst nach einer Anmeldung etwas an.",
  },
  "shell.signIn.signingIn": { en: "Signing in…", de: "Anmeldung läuft…" },
  "shell.signIn.callbackRefused": {
    en: "The authority did not complete the sign-in: {refusal}",
    de: "Die Authority hat die Anmeldung nicht abgeschlossen: {refusal}",
  },
  "shell.noProjectReach": {
    en: "Your sign-in reaches no project on this installation. An administrator grants projects under Access → Projects.",
    de: "Mit deiner Anmeldung erreichst du auf dieser Installation kein Projekt. Projekte vergibt ein Administrator unter Zugriff → Projekte.",
  },

  // --- a surface that failed or was refused ---
  "shell.failed.title": { en: "The {surface} could not be rendered.", de: "„{surface}“ ließ sich nicht darstellen." },
  "shell.failed.body": {
    en: "The rest of the page is unaffected. This is usually a payload this build does not recognise.",
    de: "Der Rest der Seite ist nicht betroffen. Meist sind es Daten, die dieser Build nicht kennt.",
  },
  "shell.failed.retry": { en: "Try again", de: "Erneut versuchen" },
  "shell.refusal.this": { en: "this", de: "das hier" },
  "shell.refusal.signedOut": { en: "You are signed out.", de: "Du bist abgemeldet." },
  "shell.refusal.asksSignIn": {
    en: "This installation asks for a sign-in before it shows {what}.",
    de: "Diese Installation verlangt eine Anmeldung, bevor sie {what} zeigt.",
  },
  "shell.refusal.notAllowed": {
    en: "You are signed in, and not allowed to see {what}.",
    de: "Du bist angemeldet, darfst {what} aber nicht sehen.",
  },
  "shell.refusal.namedPermissions": {
    en: {
      one: "The server named the permission it asked for and you do not hold:",
      other: "The server named the permissions it asked for and you do not hold:",
    },
    de: {
      one: "Der Server nennt die Berechtigung, die er verlangt und die dir fehlt:",
      other: "Der Server nennt die Berechtigungen, die er verlangt und die dir fehlen:",
    },
  },
  "shell.refusal.unnamed": {
    en: "The server named no permission — its own logs name the route it refused.",
    de: "Der Server nennt keine Berechtigung – welche Route er abgelehnt hat, steht in seinen eigenen Logs.",
  },
  "shell.refusal.whereRolesComeFrom": {
    en: "A permission comes from a role your directory grants. Your roles are on the identity page, and changing them is your directory's business rather than this dashboard's.",
    de: "Eine Berechtigung kommt aus einer Rolle, die dein Verzeichnis vergibt. Deine Rollen stehen auf der Identitätsseite; ändern kann sie dein Verzeichnis, nicht dieses Dashboard.",
  },
  "shell.surface.sidebar": { en: "navigation sidebar", de: "Navigationsleiste" },
  "shell.surface.topBar": { en: "top bar", de: "Kopfleiste" },
  "shell.surface.healthBanner": { en: "installation health banner", de: "Hinweis zum Zustand der Installation" },
  "shell.surface.buildBanner": { en: "build identity banner", de: "Hinweis zur Build-Version" },
  "shell.surface.authBanner": { en: "sign-in configuration banner", de: "Hinweis zur Anmelde-Konfiguration" },
  "shell.surface.signInGate": { en: "sign-in gate", de: "Anmeldeschranke" },
  "shell.surface.dashboard": { en: "dashboard", de: "Dashboard" },
  "shell.surface.home": { en: "home page", de: "Startseite" },
  "shell.surface.runView": { en: "run view", de: "Run-Ansicht" },
  "shell.surface.configuration": { en: "configuration", de: "Konfiguration" },
  "shell.surface.systemView": { en: "system view", de: "System-Ansicht" },
  "shell.surface.pullRequests": { en: "pull requests", de: "Pull Requests" },
  "shell.surface.ticketList": { en: "ticket list", de: "Ticketliste" },
  "shell.surface.delivery": { en: "delivery overview", de: "Delivery-Übersicht" },
  "shell.surface.workItOut": { en: "Work it out page", de: "Work-it-out-Seite" },
  "shell.surface.page": { en: "page", de: "Seite" },
  "shell.surface.run": { en: "run {run}", de: "Run {run}" },
  "shell.surface.ticket": { en: "ticket {ticket}", de: "Ticket {ticket}" },

  // --- home ---
  "home.question": { en: "What should we work on?", de: "Woran arbeiten wir?" },
  "home.waiting.none": { en: "Nothing waits on you.", de: "Nichts wartet auf dich." },
  "home.waiting.some": {
    en: { one: "One thing waits on you.", other: "{count} things wait on you." },
    de: { one: "Eine Sache wartet auf dich.", other: "{count} Sachen warten auf dich." },
  },
  "home.allWaiting": { en: "All {count} that wait on you", de: "Alle {count}, die auf dich warten" },
  "home.recentTickets": { en: "Recent tickets", de: "Zuletzt bewegte Tickets" },
  "home.allTickets": { en: "All tickets", de: "Alle Tickets" },
  "home.composer.placeholder": {
    en: "Describe new work, paste a ticket link, or add files",
    de: "Beschreib neue Arbeit, füg einen Ticket-Link ein oder häng Dateien an",
  },
  "home.composer.files": {
    en: { one: "1 file goes with your message", other: "{count} files go with your message" },
    de: { one: "1 Datei geht mit deiner Nachricht mit", other: "{count} Dateien gehen mit deiner Nachricht mit" },
  },
  "home.composer.remove": { en: "Remove", de: "Entfernen" },
  "home.composer.readingProjects": { en: "Reading the projects…", de: "Projekte werden gelesen…" },
  "home.composer.projectsFailed": { en: "The projects could not be read.", de: "Die Projekte ließen sich nicht lesen." },
  "home.composer.noProject": {
    en: "No project is configured yet. Add one in Settings to start work.",
    de: "Es ist noch kein Projekt eingerichtet. Leg eins in den Einstellungen an, dann kann es losgehen.",
  },

  // --- the overview ---
  "overview.sub": {
    en: "What the week cost, what came back, and how often a run met what was asked of it.",
    de: "Was die Woche gekostet hat, was dabei herauskam und wie oft ein Run erfüllt hat, was von ihm verlangt war.",
  },
  "overview.spend.label": { en: "Spend · 7 days", de: "Kosten · 7 Tage" },
  "overview.spend.reading": { en: "Reading the run ledger…", de: "Run-Buchungen werden gelesen…" },
  "overview.spend.today": { en: "today", de: "heute" },
  "overview.spend.calls": { en: "LLM calls", de: "LLM-Aufrufe" },
  "overview.spend.empty": {
    en: "No LLM spend recorded in the last 7 days.",
    de: "In den letzten 7 Tagen sind keine LLM-Kosten angefallen.",
  },
  "overview.spend.whereTitle": { en: "Where the money went", de: "Wohin das Geld ging" },
  "overview.spend.whereSub": {
    en: "trailing 7 days · sums to the 7-day figure",
    de: "letzte 7 Tage · ergibt in Summe den 7-Tage-Wert",
  },
  "overview.runs.succeeded": { en: "succeeded", de: "erfolgreich" },
  "overview.runs.shortfall": { en: "with a shortfall", de: "mit Lücken" },
  "overview.runs.failed": { en: "failed", de: "fehlgeschlagen" },
  "overview.runs.cancelled": { en: "cancelled", de: "abgebrochen" },
  "overview.runs.reading": { en: "Reading the run list…", de: "Run-Liste wird gelesen…" },
  "overview.criteria.label": { en: "Criteria met", de: "Kriterien erfüllt" },
  "overview.criteria.unavailable": { en: "Criteria unavailable", de: "Kriterien nicht verfügbar" },
  "overview.criteria.reading": { en: "Reading judged criteria…", de: "Bewertete Kriterien werden gelesen…" },
  "overview.criteria.noneJudged": { en: "no criterion judged yet", de: "noch kein Kriterium bewertet" },
  "overview.criteria.runs": {
    en: { one: "1 coding run", other: "{count} coding runs" },
    de: { one: "1 Coding-Run", other: "{count} Coding-Runs" },
  },

  // --- the event drawer ---
  "execution.kind.obs": { en: "obs", de: "Beob." },
  "execution.kind.find": { en: "finding", de: "Befund" },
  "execution.kind.tool": { en: "tool", de: "Tool" },
  "execution.kind.llm": { en: "llm", de: "LLM" },
  "execution.kind.file": { en: "file", de: "Datei" },
  "execution.kind.dec": { en: "decision", de: "Entscheidung" },
  "execution.filter.all": { en: "All", de: "Alle" },
  "execution.filter.find": { en: "Findings", de: "Befunde" },
  "execution.filter.obs": { en: "Obs", de: "Beob." },
  "execution.filter.tool": { en: "Tools", de: "Tools" },
  "execution.filter.llm": { en: "LLM", de: "LLM" },
  "execution.filter.file": { en: "Files", de: "Dateien" },
  "execution.filter.dec": { en: "Decisions", de: "Entscheidungen" },
  "execution.sort.newest": { en: "newest ↓", de: "neueste ↓" },
  "execution.sort.oldest": { en: "oldest ↑", de: "älteste ↑" },
  "execution.filterPlaceholder": { en: "filter content…", de: "Inhalt filtern…" },
  "execution.noMatch": { en: "No matching events.", de: "Keine passenden Ereignisse." },
  "execution.showFewer": { en: "▴ show fewer", de: "▴ weniger zeigen" },
  "execution.showAll": { en: "▾ show all {count} events", de: "▾ alle {count} Ereignisse zeigen" },

  // --- the pull-requests page ---
  "pullRequests.sub": {
    en: "Every PR agent-smith opened — per repo, linked out and back to its run.",
    de: "Jeder PR, den agent-smith geöffnet hat – pro Repo, mit Link zum PR und zurück zu seinem Run.",
  },
  "pullRequests.metric.today": { en: "Open today", de: "Heute geöffnet" },
  "pullRequests.metric.week": { en: "Open this week", de: "Diese Woche geöffnet" },
  "pullRequests.metric.total": { en: "Total open", de: "Offen insgesamt" },
  "pullRequests.surface": { en: "the pull requests", de: "die Pull Requests" },
  "pullRequests.loadFailed": {
    en: "Couldn’t load pull requests. Retry from the rail once the API is reachable.",
    de: "Die Pull Requests ließen sich nicht laden. Versuch es noch einmal, sobald die API erreichbar ist.",
  },
  "pullRequests.empty": {
    en: "No pull requests yet. When a run commits changes, its PR shows up here.",
    de: "Noch keine Pull Requests. Sobald ein Run Änderungen committet, erscheint sein PR hier.",
  },
  "pullRequests.opened.title": { en: "Opened", de: "Geöffnet" },
  "pullRequests.opened.sub": {
    en: "what the agent actually shipped — click ↗ to review",
    de: "was der Agent tatsächlich geliefert hat – ↗ zum Reviewen",
  },
  "pullRequests.attempts.title": { en: "No PR", de: "Kein PR" },
  "pullRequests.attempts.sub": {
    en: "runs that produced no pull request — and why",
    de: "Runs ohne Pull Request – und warum",
  },
  "pullRequests.attempts.noChanges": { en: "No changes to commit", de: "Keine Änderungen zum Committen" },
  "pullRequests.attempts.notOpened": { en: "PR could not be opened", de: "Der PR ließ sich nicht öffnen" },
  "pullRequests.runLink": { en: "run ›", de: "Run ›" },
  "pullRequests.status.merged": { en: "merged", de: "gemergt" },
  "pullRequests.status.autoMerging": { en: "auto-merging", de: "wird automatisch gemergt" },
  "pullRequests.status.stillOpen": { en: "still open", de: "noch offen" },
  "pullRequests.status.awaitingCi": { en: "waiting for CI", de: "wartet auf CI" },
  "pullRequests.status.noChanges": { en: "no changes", de: "keine Änderungen" },
  "pullRequests.status.failed": { en: "failed", de: "fehlgeschlagen" },
  "pullRequests.status.opened": { en: "opened", de: "geöffnet" },
  "pullRequests.ago.now": { en: "just now", de: "gerade eben" },
  "pullRequests.ago.minutes": { en: "{count}m ago", de: "vor {count} Min." },
  "pullRequests.ago.hours": { en: "{count}h ago", de: "vor {count} Std." },
  "pullRequests.ago.days": {
    en: { one: "1d ago", other: "{count}d ago" },
    de: { one: "vor 1 Tag", other: "vor {count} Tagen" },
  },
} satisfies Record<string, MessageEntry>;
