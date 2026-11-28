import type { MessageEntry } from "../messages";

// 2026-11-23-5669a: the config area's part of the one dictionary, spread into messages; English and German side by side.
// 2026-11-23-5669d: every text under components/config. Config keys, YAML values and env names stay as written.

export const configMessages = {
  // --- the sandbox timeouts the studio refuses to save (p0495) ---
  "config.refusal.runCommandOverCap": {
    en: "A run-command default of {runCommand}s cannot exceed the step cap of {cap}s: the cap only lowers a step's timeout, so the default would never apply. Raise the step cap to at least {runCommand}s, or lower the default to {cap}s or less.",
    de: "Ein Run-Command-Standard von {runCommand}s darf das Schritt-Limit von {cap}s nicht übersteigen: das Limit senkt den Timeout eines Schritts nur, der Standard würde also nie greifen. Heb das Schritt-Limit auf mindestens {runCommand}s an oder senk den Standard auf {cap}s oder weniger.",
  },

  // --- settings: tickets ---
  "config.tickets.linkDeadline.label": { en: "Requires link deadline (seconds)", de: "Frist für Requires-Links (Sekunden)" },
  "config.tickets.linkDeadline.help": {
    en: "how long one filing waits for its tracker to show the requires links",
    de: "wie lange eine Anlage wartet, bis ihr Tracker die Requires-Links zeigt",
  },
  "config.tickets.wakeWindow.label": { en: "Dependents wake window (hours)", de: "Weckfenster für abhängige Tickets (Stunden)" },
  "config.tickets.wakeWindow.help": {
    en: "landings whose waiting tickets the recovery re-wakes",
    de: "Landungen, deren wartende Tickets die Wiederherstellung erneut weckt",
  },
  "config.tickets.wakeCap.label": { en: "Dependents wake cap", de: "Obergrenze für das Wecken abhängiger Tickets" },
  "config.tickets.wakeCap.help": {
    en: "the most waiting tickets one recovery pass reads",
    de: "höchstens so viele wartende Tickets liest ein Wiederherstellungs-Durchlauf",
  },
  "config.tickets.rereadInterval.label": { en: "Re-read interval (seconds)", de: "Intervall für erneutes Lesen (Sekunden)" },
  "config.tickets.rereadInterval.help": {
    en: "time between two pull-request re-read passes",
    de: "Zeit zwischen zwei Durchläufen, die Pull Requests erneut lesen",
  },
  "config.tickets.rereadStale.label": { en: "Re-read staleness (seconds)", de: "Veraltet nach (Sekunden)" },
  "config.tickets.rereadStale.help": {
    en: "how long a pull-request record goes unchecked before the re-read names it",
    de: "wie lange ein Pull-Request-Eintrag ungeprüft bleibt, bis das erneute Lesen ihn aufgreift",
  },
  "config.tickets.rereadOpen.label": { en: "Re-read open cap", de: "Obergrenze offene Einträge" },
  "config.tickets.rereadOpen.help": {
    en: "the most open or unread records one pass names",
    de: "höchstens so viele offene oder ungelesene Einträge greift ein Durchlauf auf",
  },
  "config.tickets.rereadMerged.label": { en: "Re-read merged cap", de: "Obergrenze gemergte Einträge" },
  "config.tickets.rereadMerged.help": {
    en: "the most merged records without a landing one pass names",
    de: "höchstens so viele gemergte Einträge ohne Landung greift ein Durchlauf auf",
  },

  // --- project merge: the CI loop ---
  "config.merge.ciPoll.label": { en: "CI poll seconds", de: "CI-Abfrage (Sekunden)" },
  "config.merge.ciPoll.help": {
    en: "how often a pending CI of the open pull requests, and a running deploy, is read",
    de: "wie oft eine laufende CI der offenen Pull Requests und ein laufendes Deploy gelesen werden",
  },
  "config.merge.ciPending.label": { en: "CI pending seconds", de: "CI ausstehend (Sekunden)" },
  "config.merge.ciPending.help": {
    en: "after this a head still pending leaves the fast poll, and a deploy stops being followed",
    de: "danach verlässt ein noch ausstehender Head die schnelle Abfrage, und ein Deploy wird nicht mehr verfolgt",
  },
  "config.merge.ciSettle.label": { en: "CI settle seconds", de: "CI-Ruhezeit (Sekunden)" },
  "config.merge.ciSettle.help": {
    en: "how long 'no CI reporting' must last before it counts as none; the series master then decides",
    de: "wie lange „keine CI meldet sich“ anhalten muss, bis es als keine CI gilt; danach entscheidet der Serien-Master",
  },
  "config.merge.ciFix.label": { en: "Refresh repair passes", de: "Reparaturdurchgänge beim Aktualisieren" },
  "config.merge.ciFix.help": {
    en: "the repair passes a base refresh may make on a build or test the merged base turned red",
    de: "die Reparaturdurchgänge, die eine Basis-Aktualisierung bei einem Build oder Test machen darf, den die gemergte Basis rot gemacht hat",
  },
  "config.merge.ciExcerpt.label": { en: "CI excerpt characters", de: "Zeichen im CI-Auszug" },
  "config.merge.ciExcerpt.help": {
    en: "evidence kept per failed check: summary, then the log's end",
    de: "was pro fehlgeschlagenem Check behalten wird: Zusammenfassung, dann das Ende des Logs",
  },
  "config.merge.ciFailedChecks.label": { en: "CI failed checks read", de: "Gelesene fehlgeschlagene Checks" },
  "config.merge.ciFailedChecks.help": {
    en: "failed checks of a red head whose logs are read",
    de: "fehlgeschlagene Checks eines roten Heads, deren Logs gelesen werden",
  },
  // --- project: the machine identity (2026-11-25-9992h) ---
  "config.machine.help": {
    en: "The identity agent-smith pushes, opens pull requests, comments and merges under. A hoster left empty uses each repository's own token.",
    de: "Die Identität, unter der agent-smith pusht, Pull Requests öffnet, kommentiert und merged. Ein leerer Host nimmt das Token des jeweiligen Repos.",
  },
  "config.machine.name.label": { en: "name", de: "Name" },
  "config.machine.name.help": { en: "author of its commits and name of its acts", de: "Autor seiner Commits und Name seiner Taten" },
  "config.machine.email.label": { en: "commit email", de: "Commit-E-Mail" },
  "config.machine.email.help": { en: "the address its commits carry", de: "die Adresse in seinen Commits" },
  "config.machine.auth.label": { en: "secret", de: "Secret" },
  "config.machine.auth.placeholder": { en: "— no machine identity —", de: "— keine Maschinen-Identität —" },
  "config.machine.appId.label": { en: "GitHub App id", de: "GitHub-App-ID" },
  "config.machine.appId.help": {
    en: "set for a GitHub App; the secret is then its private key",
    de: "für eine GitHub App; das Secret ist dann ihr privater Schlüssel",
  },
  "config.machine.installationId.label": { en: "installation id", de: "Installations-ID" },
  "config.machine.tenantId.label": { en: "Entra tenant id", de: "Entra-Mandanten-ID" },
  "config.machine.tenantId.help": {
    en: "with a client id, a service principal: the secret is its client secret",
    de: "mit einer Client-ID ein Service Principal: das Secret ist sein Client-Secret",
  },
  "config.machine.clientId.label": { en: "client id", de: "Client-ID" },
  "config.pocs.machine.title": { en: "Machine identity", de: "Maschinen-Identität" },
  "config.pocs.machine.help": {
    en: "Who the PoC pipelines, commits and builds act as. A poc- project's own machine identity wins.",
    de: "Als wer die PoC-Pipelines, Commits und Builds handeln. Die eigene Maschinen-Identität eines poc-Projekts geht vor.",
  },
  "config.machine.hosterEmail.label": { en: "commit email here", de: "Commit-E-Mail hier" },
  "config.machine.hosterEmail.help": {
    en: "when it differs on this hoster, e.g. a GitHub App's bot address",
    de: "wenn sie bei diesem Host abweicht, z. B. die Bot-Adresse einer GitHub App",
  },

  // --- tracker: the form's tabs ---
  "config.tracker.tab.connection": { en: "Connection", de: "Verbindung" },
  "config.tracker.tab.intake": { en: "Intake", de: "Eingang" },
  "config.tracker.tab.outcomes": { en: "Outcomes", de: "Ergebnisse" },
  "config.tracker.tab.routing": { en: "Routing", de: "Routing" },
  "config.tracker.tab.filing": { en: "Filing", de: "Anlage" },

  // --- secrets: values left behind by a deleted secret ---
  "config.secretOrphans.title": { en: "Stored values without a secret", de: "Gespeicherte Werte ohne Secret" },
  "config.secretOrphans.setBy": { en: "set by {by} at {at}", de: "gesetzt von {by} am {at}" },

  // --- settings: spec review and series ---
  "config.specReview.reground.label": { en: "Re-ground read budget", de: "Lesebudget für die Nachprüfung" },
  "config.specReview.reground.help": {
    en: "file reads that re-check the citations of the spec a run enters; past it a citation is left unchecked",
    de: "Dateilesevorgänge, die die Zitate der Spec prüfen, mit der ein Run startet; darüber hinaus bleibt ein Zitat ungeprüft",
  },
  "config.series.maxSpecs.label": { en: "Max specs", de: "Max. Specs" },
  "config.series.maxSpecs.help": { en: "the most specs one ticket's series holds", de: "höchstens so viele Specs umfasst die Serie eines Tickets" },
  "config.series.perAttempt.label": { en: "Specs per attempt", de: "Specs pro Versuch" },
  "config.series.perAttempt.help": {
    en: "the most specs one attempt runs; fewer when the measured spec block does not fit the wall clock left",
    de: "höchstens so viele Specs führt ein Versuch aus; weniger, wenn der gemessene Spec-Block nicht in die verbleibende Laufzeit passt",
  },
  "config.series.secondsPerSpec.label": { en: "Seconds per spec", de: "Sekunden pro Spec" },
  "config.series.secondsPerSpec.help": {
    en: "the whole spec block — build, review, verify; 0 = not measured",
    de: "der ganze Spec-Block — Bauen, Review, Prüfen; 0 = nicht gemessen",
  },
  "config.series.budget.label": { en: "Budget (USD)", de: "Budget (USD)" },
  "config.series.budget.help": {
    en: "spend of one chain of attempts before the next is held; 0 = off, no attempt starts by itself",
    de: "Kosten einer Kette von Versuchen, bevor der nächste angehalten wird; 0 = aus, kein Versuch startet von selbst",
  },
  "config.series.wallClock.label": { en: "Wall clock (hours of run time)", de: "Laufzeit (Stunden)" },
  "config.series.wallClock.help": {
    en: "summed run time of one chain of attempts; time held between attempts does not count",
    de: "summierte Laufzeit einer Kette von Versuchen; Wartezeit zwischen den Versuchen zählt nicht",
  },
  "config.series.continue.label": { en: "Continue after a wall-time cancel", de: "Nach Abbruch wegen Laufzeit weitermachen" },
  "config.series.buildNote.label": { en: "Build note characters", de: "Zeichen der Build-Notizen" },
  "config.series.buildNote.help": {
    en: "earlier specs' build notes the next spec is shown; past it the oldest are dropped and named",
    de: "Build-Notizen früherer Specs, die die nächste Spec sieht; darüber hinaus fallen die ältesten weg und werden genannt",
  },

  // --- sandbox: the image boundary in force ---
  "config.imageTrust.readFailed": {
    en: "The boundary in force could not be read: {message}",
    de: "Die geltende Grenze ließ sich nicht lesen: {message}",
  },
  "config.imageTrust.reading": { en: "Reading the boundary in force…", de: "Geltende Grenze wird gelesen…" },
  "config.imageTrust.inForce": { en: "In force now", de: "Gilt jetzt" },
  "config.imageTrust.unsaved": {
    en: "unsaved changes — this is what runs use until you save",
    de: "ungespeicherte Änderungen — das hier nutzen Runs, bis du speicherst",
  },
  "config.imageTrust.default": {
    en: "the built-in default — no registry is named",
    de: "der eingebaute Standard — keine Registry angegeben",
  },
  "config.imageTrust.named": {
    en: "the registries named here, replacing the built-in default",
    de: "die hier genannten Registries, anstelle des eingebauten Standards",
  },

  // --- connections: repository discovery ---
  "config.discovery.discovering": { en: "discovering…", de: "wird erkundet…" },
  "config.discovery.lastSuccess": { en: "last success {at} · {count} repos", de: "zuletzt erfolgreich {at} · {count} Repos" },
  "config.discovery.noSuccess": { en: "no successful discovery yet", de: "noch keine erfolgreiche Erkundung" },
  "config.discovery.lastAttemptAtFailed": {
    en: "last attempt {at} failed: {error}",
    de: "letzter Versuch {at} fehlgeschlagen: {error}",
  },
  "config.discovery.lastAttemptFailed": { en: "last attempt failed: {error}", de: "letzter Versuch fehlgeschlagen: {error}" },
  "config.discovery.refreshing": { en: "Refreshing…", de: "Wird aktualisiert…" },
  "config.discovery.refresh": { en: "Refresh now", de: "Jetzt aktualisieren" },
  "config.discovery.refreshFailed": { en: "refresh failed: {message}", de: "Aktualisieren fehlgeschlagen: {message}" },

  // --- secrets: the value set in the admin area ---
  "config.secret.keySource": { en: "Master key: {source}, set by the deployment.", de: "Master-Key: {source}, vom Deployment gesetzt." },
  "config.secret.valueAria": { en: "Secret value", de: "Wert des Secrets" },
  "config.secret.replacePlaceholder": { en: "enter a new value to replace it", de: "neuen Wert eingeben, um ihn zu ersetzen" },
  "config.secret.set": { en: "Set value", de: "Wert setzen" },
  "config.secret.deleteStored": { en: "Delete stored value", de: "Gespeicherten Wert löschen" },
  "config.secret.rewrap": { en: "Re-wrap with the current master key", de: "Mit dem aktuellen Master-Key neu verschlüsseln" },
  "config.secret.state.noneYet": { en: "No value stored here yet.", de: "Hier ist noch kein Wert gespeichert." },
  "config.secret.state.stored": { en: "Stored value set by {by} at {at}", de: "Gespeicherter Wert, gesetzt von {by} am {at}" },
  "config.secret.state.storedUnreadable": {
    en: "Stored value set by {by} at {at} — the running master key cannot open it",
    de: "Gespeicherter Wert, gesetzt von {by} am {at} — der laufende Master-Key kann ihn nicht öffnen",
  },
  "config.secret.state.none": { en: "No value stored here", de: "Hier ist kein Wert gespeichert" },
  "config.secret.state.sentence": { en: "{stored}.", de: "{stored}." },
  "config.secret.state.environmentWins": {
    en: "{stored}. The environment variable {variable} is set and wins.",
    de: "{stored}. Die Umgebungsvariable {variable} ist gesetzt und hat Vorrang.",
  },

  // --- shared within the studio ---
  "config.pick": { en: "— pick —", de: "— auswählen —" },
  "config.remove": { en: "Remove", de: "Entfernen" },

  // --- project sandbox: secrets injected into the pod ---
  "config.sandboxSecrets.noneInherited": {
    en: "Nothing is inherited here: there is no process-wide secrets block to fall back to, so this project declares these or it has none. Names only — the values stay in the cluster and this product never sees one.",
    de: "Hier wird nichts geerbt: es gibt keinen prozessweiten Secrets-Block als Rückfall, also deklariert dieses Projekt sie selbst oder hat keine. Nur Namen — die Werte bleiben im Cluster, und dieses Produkt sieht keinen davon.",
  },
  "config.sandboxSecrets.env.label": { en: "secret environment variables", de: "geheime Umgebungsvariablen" },
  "config.sandboxSecrets.env.help": {
    en: "environment variable name, and the secretName:key it is taken from",
    de: "Name der Umgebungsvariable und der secretName:key, aus dem sie stammt",
  },
  "config.sandboxSecrets.repoEnv.label": { en: "secret variables for one repository", de: "geheime Variablen für ein Repository" },
  "config.sandboxSecrets.repoEnv.help": {
    en: "only that repository's commands get the variable; needs a current sandbox agent (Kubernetes)",
    de: "nur die Befehle dieses Repositorys bekommen die Variable; braucht einen aktuellen Sandbox-Agent (Kubernetes)",
  },
  "config.sandboxSecrets.repoEnv.repo": { en: "repository", de: "Repository" },
  "config.sandboxSecrets.repoEnv.name": { en: "variable", de: "Variable" },
  "config.sandboxSecrets.repoEnv.ref": { en: "secretName:key", de: "secretName:key" },
  "config.sandboxSecrets.repoEnv.none": { en: "none", de: "keine" },
  "config.sandboxSecrets.repoEnv.add": { en: "Add a variable", de: "Variable hinzufügen" },
  "config.sandboxSecrets.files.label": { en: "mounted secret files", de: "eingehängte Secret-Dateien" },
  "config.sandboxSecrets.files.help": {
    en: "mount path, the secret holding the value, and the key projected as the file",
    de: "Mount-Pfad, das Secret mit dem Wert und der Key, der als Datei eingeblendet wird",
  },

  // --- tracker: outcome statuses ---
  "config.tracker.outcomes.head": { en: "When a run ends, move the ticket to", de: "Wenn ein Run endet, verschiebe das Ticket nach" },
  "config.tracker.outcomes.sharedTwo": {
    en: "{first} and {second} both move the ticket to",
    de: "{first} und {second} verschieben das Ticket beide nach",
  },
  "config.tracker.outcomes.sharedMany": {
    en: "{list} and {last} all move the ticket to",
    de: "{list} und {last} verschieben das Ticket alle nach",
  },
  "config.tracker.outcomes.cannotTell": {
    en: "— the board cannot tell these outcomes apart.",
    de: "— das Board kann diese Ergebnisse nicht unterscheiden.",
  },

  // --- project merge: start and merge mode ---
  "config.merge.requiresStart.label": { en: "requires start", de: "Start bei Abhängigkeiten" },
  "config.merge.requiresStart.placeholder": { en: "— default (on_green_push) —", de: "— Standard (on_green_push) —" },
  "config.merge.requiresStart.help": {
    en: "when a ticket that requires another may start: once each prerequisite pushed a green branch, or once each merged",
    de: "wann ein Ticket, das ein anderes voraussetzt, starten darf: sobald jede Voraussetzung einen grünen Branch gepusht hat oder sobald jede gemergt ist",
  },
  // 2026-11-25-9992i: the one merge decision
  "config.merge.review.label": { en: "review", de: "Review" },
  "config.merge.review.help": {
    en: "when a person looks. end: a series collects its tickets on a series branch and you review one pull request per repository at the end. each: you approve every ticket's pull request. none: agent-smith merges on green, and the host's branch protection still decides",
    de: "wann ein Mensch draufschaut. end: eine Serie sammelt ihre Tickets auf einem Serien-Branch, und du prüfst am Ende einen Pull Request pro Repository. each: du gibst jeden Ticket-Pull-Request frei. none: agent-smith mergt bei Grün, und der Branch-Schutz des Hosts entscheidet weiterhin",
  },

  // --- project pipeline: how a ticket is routed ---
  "config.trackerRouting.title": { en: "how a ticket reaches a pipeline", de: "wie ein Ticket zu einer Pipeline kommt" },
  "config.trackerRouting.ownedBy": { en: "owned by tracker {tracker} — edit it there", de: "gehört zu Tracker {tracker} — dort bearbeiten" },
  "config.trackerRouting.pickTracker": { en: "pick a tracker first", de: "wähl zuerst einen Tracker" },
  "config.trackerRouting.noTracker": {
    en: "this project names no tracker yet, so nothing routes to it",
    de: "dieses Projekt nennt noch keinen Tracker, also wird nichts hierher geleitet",
  },
  "config.trackerRouting.noMatch": {
    en: "a ticket whose label matches none of these is not routed to this project at all.",
    de: "ein Ticket, dessen Label zu keinem davon passt, wird gar nicht zu diesem Projekt geleitet.",
  },
  "config.trackerRouting.declared": {
    en: "this tracker maps no label, so every ticket it routes here runs {pipeline} — what this tracker declares.",
    de: "dieser Tracker ordnet kein Label zu, also läuft jedes Ticket, das er hierher leitet, mit {pipeline} — was dieser Tracker deklariert.",
  },
  "config.trackerRouting.undeclared": {
    en: "this tracker maps no label and declares no default, so every ticket it routes here runs {pipeline} — nothing declares that; set a default pipeline on the tracker.",
    de: "dieser Tracker ordnet kein Label zu und deklariert keinen Standard, also läuft jedes Ticket, das er hierher leitet, mit {pipeline} — das deklariert niemand; setz eine Standard-Pipeline am Tracker.",
  },

  // --- tracker: polling ---
  "config.polling.label": { en: "polling", de: "Polling" },
  "config.polling.backendDefault": { en: "backend default applies", de: "Standard des Backends gilt" },
  "config.polling.configure": { en: "Configure polling", de: "Polling einrichten" },
  "config.polling.interval": { en: "poll interval (seconds)", de: "Polling-Intervall (Sekunden)" },
  "config.polling.jitter": { en: "jitter (%)", de: "Jitter (%)" },
  "config.polling.remove": { en: "Remove polling override", de: "Polling-Überschreibung entfernen" },

  // --- sandbox: trusted image registries ---
  "config.imageTrust.title": { en: "Trusted image registries", de: "Vertrauenswürdige Image-Registries" },
  "config.imageTrust.namedCount": { en: "{count} named", de: "{count} genannt" },
  "config.imageTrust.defaultSummary": { en: "default", de: "Standard" },
  "config.imageTrust.note": {
    en: "Where a context's stack.image may come from. Each entry is a prefix of the image reference as Docker reads it — an image with no registry host is on docker.io, so sbtscala/ and docker.io/sbtscala/ are the same entry, and docker.io/ alone trusts all of Docker Hub; docker.io/library/ is its official images (node:22-bookworm). Only the stack.image a context declares is judged: an image named in a project's sandbox settings and the built-in fallbacks are not. A refused image falls back to the language default. Applies to the next decision, no restart.",
    de: "Woher das stack.image eines Kontexts kommen darf. Jeder Eintrag ist ein Präfix der Image-Referenz, wie Docker sie liest — ein Image ohne Registry-Host liegt auf docker.io, also sind sbtscala/ und docker.io/sbtscala/ derselbe Eintrag, und docker.io/ allein vertraut ganz Docker Hub; docker.io/library/ sind dessen offizielle Images (node:22-bookworm). Geprüft wird nur das stack.image, das ein Kontext deklariert: ein Image aus den Sandbox-Einstellungen eines Projekts und die eingebauten Rückfälle nicht. Ein abgelehntes Image fällt auf den Standard der Sprache zurück. Gilt ab der nächsten Entscheidung, ohne Neustart.",
  },
  "config.imageTrust.empty": {
    en: "No registry named — the built-in default applies. Adding the first entry starts from it.",
    de: "Keine Registry genannt — der eingebaute Standard gilt. Der erste Eintrag geht von ihm aus.",
  },
  "config.imageTrust.emptyWithDefaults": {
    en: "No registry named — the built-in default applies: {defaults}. Adding the first entry starts from it.",
    de: "Keine Registry genannt — der eingebaute Standard gilt: {defaults}. Der erste Eintrag geht von ihm aus.",
  },
  "config.imageTrust.add": { en: "Add a registry", de: "Registry hinzufügen" },
  "config.imageTrust.allowNew.label": { en: "Allow new registries", de: "Neue Registries erlauben" },
  "config.imageTrust.allowNew.note": {
    en: "A ticket run adds the registry of an untrusted image to this list and records it.",
    de: "Ein Ticket-Run fügt die Registry eines nicht vertrauenswürdigen Images dieser Liste hinzu und hält das fest.",
  },

  // --- deployment binding: environments ---
  "config.environments.title": { en: "environments", de: "Umgebungen" },
  "config.environments.help": {
    en: "where this context is delivered, by the names its deployment template declares",
    de: "wohin dieser Kontext ausgeliefert wird, mit den Namen, die sein Deployment-Template deklariert",
  },
  "config.environments.add": { en: "Add environment", de: "Umgebung hinzufügen" },
  "config.environments.name": { en: "name", de: "Name" },
  "config.environments.repo.label": { en: "manifest repo", de: "Manifest-Repo" },
  "config.environments.repo.placeholder": { en: "the context's own repository", de: "das eigene Repository des Kontexts" },
  "config.environments.approval.label": { en: "a person approves", de: "ein Mensch gibt frei" },
  "config.environments.approval.help": {
    en: "on: agent-smith opens the delivery's pull request and a person merges it; off: agent-smith merges it too",
    de: "an: agent-smith öffnet den Pull Request der Auslieferung und ein Mensch mergt ihn; aus: agent-smith mergt ihn auch",
  },
  "config.environments.parameters.label": { en: "parameters", de: "Parameter" },
  "config.environments.parameters.help": {
    en: "the template's parameter names and their values; never a secret",
    de: "die Parameternamen des Templates und ihre Werte; nie ein Secret",
  },
  "config.environments.removeAria": { en: "Remove environment {name}", de: "Umgebung {name} entfernen" },
  "config.environments.remove": { en: "Remove environment", de: "Umgebung entfernen" },

  // --- the studio's navigation ---
  "config.nav.settings": { en: "Settings", de: "Einstellungen" },
  "config.nav.catalog": { en: "Catalog", de: "Katalog" },
  "config.nav.access": { en: "Access", de: "Zugriff" },
  "config.nav.permissions": { en: "Permissions", de: "Berechtigungen" },
  "config.nav.thisInstallation": { en: "This installation", de: "Diese Installation" },
  "config.nav.installation": { en: "Installation", de: "Installation" },
  "config.nav.connectionCheck": { en: "Connection check", de: "Verbindungstest" },
  "config.nav.changes": { en: "Changes", de: "Änderungen" },

  // --- the entity kinds ---
  "config.kind.agents": { en: "Agents", de: "Agents" },
  "config.kind.trackers": { en: "Trackers", de: "Tracker" },
  "config.kind.connections": { en: "Connections", de: "Verbindungen" },
  "config.kind.repos": { en: "Repositories", de: "Repositories" },
  "config.kind.projects": { en: "Projects", de: "Projekte" },
  "config.kind.mcpServers": { en: "MCP servers", de: "MCP-Server" },
  "config.kind.designSources": { en: "Design sources", de: "Design-Quellen" },
  "config.kind.secrets": { en: "Secrets", de: "Secrets" },
  "config.kind.agent": { en: "Agent", de: "Agent" },
  "config.kind.tracker": { en: "Tracker", de: "Tracker" },
  "config.kind.connection": { en: "Connection", de: "Verbindung" },
  "config.kind.repo": { en: "Repository", de: "Repository" },
  "config.kind.project": { en: "Project", de: "Projekt" },
  "config.kind.mcpServer": { en: "MCP server", de: "MCP-Server" },
  "config.kind.designSource": { en: "Design source", de: "Design-Quelle" },
  "config.kind.secret": { en: "Secret", de: "Secret" },
  "config.kind.settings": { en: "Settings", de: "Einstellungen" },
  "config.kind.agents.sub": { en: "LLM providers and their per-role models", de: "LLM-Provider und ihre Modelle pro Rolle" },
  "config.kind.trackers.sub": { en: "ticket sources — polling and webhook triggers", de: "Ticketquellen — Polling- und Webhook-Trigger" },
  "config.kind.connections.sub": {
    en: "repo-discovery scopes — org/project + auth",
    de: "Bereiche für die Repo-Erkundung — Org/Projekt + Zugang",
  },
  "config.kind.repos.sub": {
    en: "individual repositories the pipelines work on",
    de: "einzelne Repositories, an denen die Pipelines arbeiten",
  },
  "config.kind.projects.sub": { en: "the wiring — agent → project ← tracker · repos", de: "die Verdrahtung — Agent → Projekt ← Tracker · Repos" },
  "config.kind.mcpServers.sub": { en: "external MCP tool servers", de: "externe MCP-Tool-Server" },
  "config.kind.designSources.sub": {
    en: "design tools projects read designs from — token by secret name",
    de: "Design-Tools, aus denen Projekte Designs lesen — Token per Secret-Name",
  },
  "config.kind.secrets.sub": {
    en: "env-names only — values stay in the runtime store",
    de: "nur Umgebungsnamen — die Werte bleiben im Laufzeit-Speicher",
  },
  "config.badge.agent": { en: "agent", de: "Agent" },
  "config.badge.tracker": { en: "tracker", de: "Tracker" },
  "config.badge.connection": { en: "connection", de: "Verbindung" },
  "config.badge.repo": { en: "repo", de: "Repo" },
  "config.badge.project": { en: "project", de: "Projekt" },
  "config.badge.mcp": { en: "mcp", de: "MCP" },
  "config.badge.design": { en: "design", de: "Design" },
  "config.badge.secret": { en: "secret", de: "Secret" },

  // --- the settings singletons ---
  "config.setting.orchestrator": { en: "Orchestrator", de: "Orchestrator" },
  "config.setting.sandbox": { en: "Sandbox", de: "Sandbox" },
  "config.setting.deployment": { en: "Deployment", de: "Deployment" },
  "config.setting.primaryProvider": { en: "Primary provider", de: "Primärer Provider" },
  "config.setting.limits": { en: "Limits", de: "Limits" },
  "config.setting.pipelineCostCap": { en: "Pipeline cost cap", de: "Kostenlimit der Pipeline" },
  "config.setting.queue": { en: "Queue", de: "Warteschlange" },
  "config.setting.dialogue": { en: "Dialogue", de: "Dialog" },
  "config.setting.specReview": { en: "Spec review", de: "Spec-Review" },
  "config.setting.series": { en: "Series", de: "Serien" },
  "config.setting.preconditions": { en: "Preconditions", de: "Vorbedingungen" },
  "config.setting.branchCleanup": { en: "Branch cleanup", de: "Branch-Bereinigung" },
  "config.setting.tickets": { en: "Ticket limits", de: "Ticket-Limits" },
  "config.setting.holds": { en: "Holds", de: "Holds" },
  "config.setting.pocs": { en: "PoCs", de: "PoCs" },
  "config.setting.skills": { en: "Skills", de: "Skills" },
  "config.setting.pipelineDataFlow": { en: "Pipeline data flow", de: "Datenfluss der Pipeline" },
  "config.setting.trace": { en: "Trace", de: "Trace" },
  // 2026-11-28-9bd1: the settings groups and what a group page says about its sections.
  "config.settingGroup.runs": { en: "Runs & limits", de: "Runs & Limits" },
  "config.settingGroup.runs.sub": {
    en: "how long a run may take, what it may spend and call, the queue it waits in and the image it runs",
    de: "wie lange ein Run dauern darf, was er ausgeben und aufrufen darf, seine Warteschlange und sein Image",
  },
  "config.settingGroup.workflow": { en: "Workflow", de: "Ablauf" },
  "config.settingGroup.workflow.sub": {
    en: "the waits, re-checks and sweeps around a ticket's work",
    de: "das Warten, erneute Prüfen und Aufräumen rund um die Arbeit an einem Ticket",
  },
  "config.settingGroup.diagnostics": { en: "Diagnostics", de: "Diagnose" },
  "config.settingGroup.diagnostics.sub": {
    en: "what a run records about itself",
    de: "was ein Run über sich selbst aufzeichnet",
  },
  "config.settingGroup.changed": { en: "{count} changed", de: "{count} geändert" },
  "config.settingGroup.onlyChanged": { en: "Show only changed values", de: "Nur geänderte Werte zeigen" },
  "config.settingGroup.noneChanged": {
    en: "Every value here is its default.",
    de: "Jeder Wert hier hat seinen Standardwert.",
  },
  "config.setting.orchestrator.sub": { en: "the run wall-time ceiling", de: "die Obergrenze der Laufzeit eines Runs" },
  "config.setting.sandbox.sub": {
    en: "sandbox agent image, per-step / per-command timeouts and trusted image registries",
    de: "Image des Sandbox-Agents, Timeouts pro Schritt / pro Befehl und vertrauenswürdige Image-Registries",
  },
  "config.setting.deployment.sub": {
    en: "the image pin feeding the sandbox agent when it names none",
    de: "der Image-Pin für den Sandbox-Agent, wenn er selbst keinen nennt",
  },
  "config.setting.primaryProvider.sub": {
    en: "the default agent provider when a project names none",
    de: "der Standard-Provider des Agents, wenn ein Projekt keinen nennt",
  },
  "config.setting.limits.sub": {
    en: "per-skill agentic loop ceilings — tool calls, tokens, sub-agents",
    de: "Obergrenzen der Agenten-Schleife pro Skill — Tool-Aufrufe, Tokens, Sub-Agents",
  },
  "config.setting.pipelineCostCap.sub": {
    en: "USD / token budget per run — a default plus per-pipeline and per-tier caps",
    de: "USD-/Token-Budget pro Run — ein Standard plus Limits pro Pipeline und pro Stufe",
  },
  "config.setting.queue.sub": {
    en: "consumer backpressure and Redis retry cadence",
    de: "Gegendruck der Consumer und Wiederholtakt für Redis",
  },
  "config.setting.dialogue.sub": {
    en: "hot-wait window and approval timeout for human dialogue",
    de: "Wartefenster und Freigabe-Timeout für den Dialog mit Menschen",
  },
  "config.setting.specReview.sub": {
    en: "the file reads that re-check the citations of the spec a run enters",
    de: "die Dateilesevorgänge, die die Zitate der Spec prüfen, mit der ein Run startet",
  },
  "config.setting.series.sub": {
    en: "how many specs a series holds and one attempt runs, and its budget for starting attempts by itself",
    de: "wie viele Specs eine Serie umfasst und ein Versuch ausführt, und ihr Budget, um Versuche von selbst zu starten",
  },
  "config.setting.preconditions.sub": {
    en: "how often a ticket stuck on a precondition is checked again, and how far back an interrupted run is restarted by itself",
    de: "wie oft ein Ticket, das an einer Vorbedingung hängt, erneut geprüft wird, und wie weit zurück ein unterbrochener Run von selbst neu startet",
  },
  "config.setting.branchCleanup.sub": {
    en: "the sweep that deletes finished agent-smith branches, and how often it runs",
    de: "der Durchlauf, der fertige agent-smith Branches löscht, und wie oft er läuft",
  },
  "config.setting.tickets.sub": {
    en: "requires links on the tracker, the dependents wake and the pull-request re-read",
    de: "Requires-Links im Tracker, das Wecken abhängiger Tickets und das erneute Lesen von Pull Requests",
  },
  "config.setting.holds.sub": {
    en: "how long a ticket may wait on nobody before the wait is reported to the admin",
    de: "wie lange ein Ticket auf niemanden warten darf, bevor die Wartezeit dem Admin gemeldet wird",
  },
  "config.setting.pocs.sub": {
    en: "whether people can host PoCs here, and where their source and apps go",
    de: "ob hier PoCs gehostet werden dürfen und wohin ihre Quellen und Apps gehen",
  },
  "config.setting.skills.sub": { en: "where the skill catalog is resolved from", de: "woher der Skill-Katalog aufgelöst wird" },
  "config.setting.pipelineDataFlow.sub": {
    en: "data-flow gating — warn only, or enforce",
    de: "Datenfluss-Prüfung — nur warnen oder durchsetzen",
  },
  "config.setting.trace.sub": {
    en: "record each run's conversation, not only its numbers — AGENTSMITH_TRACE overrides it",
    de: "die Unterhaltung jedes Runs aufzeichnen, nicht nur seine Zahlen — AGENTSMITH_TRACE überschreibt das",
  },
  "config.required": { en: "required", de: "Pflicht" },
  "config.needsAttention": { en: "needs attention", de: "braucht Aufmerksamkeit" },
  "config.capabilitiesUnavailable": { en: "capabilities unavailable", de: "Fähigkeiten nicht verfügbar" },
  "config.removeRow": { en: "Remove {label} row {n}", de: "{label} Zeile {n} entfernen" },

  // --- agent: model id search ---
  "config.model.id.label": { en: "model it runs", de: "Modell, das läuft" },
  "config.model.id.help": {
    en: "search the price list; the deployment name is separate",
    de: "in der Preisliste suchen; der Deployment-Name ist getrennt",
  },
  "config.model.id.placeholder": { en: "e.g. gpt-4.1", de: "z. B. gpt-4.1" },
  "config.model.id.matchesAria": { en: "price list matches", de: "Treffer in der Preisliste" },

  // --- connections and trackers: Test ---
  "config.draftCheck.label": { en: "test", de: "Test" },
  "config.draftCheck.help": {
    en: "checks this unsaved draft against its host; nothing is saved",
    de: "prüft diesen ungespeicherten Entwurf gegen seinen Host; nichts wird gespeichert",
  },
  "config.draftCheck.running": { en: "Testing…", de: "Wird getestet…" },
  "config.draftCheck.run": { en: "Test", de: "Testen" },
  "config.draftCheck.passed": { en: "passed", de: "bestanden" },
  "config.draftCheck.failed": { en: "failed", de: "fehlgeschlagen" },

  // --- project sandbox: mounted secret files ---
  "config.secretFiles.mount": { en: "mount path", de: "Mount-Pfad" },
  "config.secretFiles.secret": { en: "secret name", de: "Secret-Name" },
  "config.secretFiles.key": { en: "key in the secret", de: "Key im Secret" },
  "config.secretFiles.help": { en: "mount path, secret name and key — no value", de: "Mount-Pfad, Secret-Name und Key — kein Wert" },
  "config.secretFiles.none": { en: "no mounted secret files", de: "keine eingehängten Secret-Dateien" },
  "config.secretFiles.add": { en: "+ add file mount", de: "+ Datei-Mount hinzufügen" },

  // --- agent: provider tab ---
  "config.agent.provider": { en: "provider", de: "Provider" },
  "config.agent.keySecret": { en: "key secret", de: "Key-Secret" },
  "config.agent.endpoint": { en: "endpoint", de: "Endpoint" },
  "config.agent.apiVersion": { en: "api version", de: "API-Version" },
  "config.agent.networkTimeout": { en: "network timeout (seconds)", de: "Netzwerk-Timeout (Sekunden)" },
  "config.agent.maxConcurrentRuns.label": { en: "runs at a time on this model", de: "gleichzeitige Runs auf diesem Modell" },
  "config.agent.maxConcurrentRuns.placeholder": { en: "no limit", de: "kein Limit" },
  "config.agent.maxConcurrentRuns.help": {
    en: "max_concurrent_runs: a run and all its sub-agents count once; other runs wait. Set 1 for a shared deployment whose quota one run fills.",
    de: "max_concurrent_runs: ein Run und all seine Sub-Agents zählen einmal; andere Runs warten. Setz 1 für ein geteiltes Deployment, dessen Kontingent ein Run ausschöpft.",
  },

  // --- agent: the drawer's tabs ---
  "config.agent.sections": { en: "Agent sections", de: "Agent-Bereiche" },
  "config.agent.tab.models": { en: "Models · {count}", de: "Modelle · {count}" },
  "config.agent.tab.activities": { en: "Activities", de: "Aktivitäten" },
  "config.agent.tab.provider": { en: "Provider", de: "Provider" },
  "config.agent.tab.tuning": { en: "Tuning", de: "Feintuning" },
  "config.pickFromCatalog": { en: "pick from the catalog", de: "aus dem Katalog wählen" },
  "config.noCatalogEntries": { en: "no entries in catalog", de: "keine Einträge im Katalog" },
  "config.nothingToPick": { en: "nothing to pick from", de: "nichts zur Auswahl" },
  "config.enabled": { en: "enabled", de: "aktiviert" },
  "config.disabled": { en: "disabled", de: "deaktiviert" },
  "config.type": { en: "type", de: "Typ" },
  "config.loading": { en: "Loading…", de: "Wird geladen…" },
  "config.saving": { en: "Saving…", de: "Wird gespeichert…" },
  "config.saveChanges": { en: "Save changes", de: "Änderungen speichern" },
  "config.saveFailed": { en: "Save failed: {message}", de: "Speichern fehlgeschlagen: {message}" },
  "config.settings.surface": { en: "these settings", de: "diese Einstellungen" },
  "config.settings.loadFailed": {
    en: "Failed to load these settings: {message}",
    de: "Diese Einstellungen ließen sich nicht laden: {message}",
  },
  "config.tracker.sections": { en: "Tracker sections", de: "Tracker-Bereiche" },

  // --- templates: context names read from repositories ---
  "config.context.fromDefaultBranch": {
    en: "read from the repository's default branch",
    de: "aus dem Standard-Branch des Repositorys gelesen",
  },
  "config.context.unreadable": { en: "could not read {subject}: {lines}", de: "{subject} ließ sich nicht lesen: {lines}" },
  "config.context.subjectProjectRepos": { en: "this project's repositories", de: "die Repositories dieses Projekts" },
  "config.context.label": { en: "context", de: "Kontext" },
  "config.context.reading": { en: "reading {subject}...", de: "lese {subject}..." },
  "config.context.noList": { en: "no list to pick from - type the name", de: "keine Liste zur Auswahl - gib den Namen ein" },
  "config.context.reposDeclareNone": {
    en: "{subject} declare no context - type the name",
    de: "{subject}: kein Kontext deklariert - gib den Namen ein",
  },
  "config.context.repoDeclaresNone": {
    en: "{subject} declares no context - type the name",
    de: "{subject}: kein Kontext deklariert - gib den Namen ein",
  },
  "config.context.sharedNames": {
    en: "read from the repository's default branch — {names} declared by more than one",
    de: "aus dem Standard-Branch des Repositorys gelesen — {names} von mehr als einem deklariert",
  },

  // --- templates: one binding ---
  "config.template.kind.label": { en: "kind", de: "Art" },
  "config.template.kind.code": { en: "code — how this context is built", de: "Code — wie dieser Kontext gebaut wird" },
  "config.template.kind.deployment": {
    en: "deployment — how this context is delivered",
    de: "Deployment — wie dieser Kontext ausgeliefert wird",
  },
  "config.template.project.label": { en: "built after project", de: "gebaut nach Projekt" },
  "config.template.repo.label": { en: "its repo", de: "sein Repo" },
  "config.template.repo.pickProject": { en: "pick a project first", de: "wähl zuerst ein Projekt" },
  "config.template.context.label": { en: "its context", de: "sein Kontext" },
  "config.template.context.subject": { en: "that repository", de: "dieses Repository" },
  "config.template.revision.label": { en: "revision", de: "Revision" },
  "config.template.revision.placeholder": { en: "a tag, a branch or a commit", de: "ein Tag, ein Branch oder ein Commit" },
  "config.template.revision.help": {
    en: "typed, not picked — nothing lists refs; it is verified where the template is fetched",
    de: "getippt, nicht gewählt — nichts listet Refs auf; geprüft wird, wo das Template geholt wird",
  },
  "config.template.targetRepo": { en: "the target repository", de: "das Ziel-Repository" },

  // --- templates: the list ---
  "config.templates.label": { en: "templates", de: "Templates" },
  "config.templates.help": {
    en: "what each context of this project is built after",
    de: "wonach jeder Kontext dieses Projekts gebaut wird",
  },
  "config.templates.helpDeclared": {
    en: "what each context of this project is built after — {count} declared",
    de: "wonach jeder Kontext dieses Projekts gebaut wird — {count} deklariert",
  },
  "config.templates.none": { en: "this project declares no template", de: "dieses Projekt deklariert kein Template" },
  "config.templates.thisProject": { en: "this project", de: "dieses Projekt" },
  "config.templates.add": { en: "Add template", de: "Template hinzufügen" },
  "config.branch": { en: "branch {branch}", de: "Branch {branch}" },

  // --- templates: one row ---
  "config.templateRow.collapse": { en: "Collapse template {n}", de: "Template {n} zuklappen" },
  "config.templateRow.edit": { en: "Edit template {n}", de: "Template {n} bearbeiten" },
  "config.templateRow.unfinished": { en: "not finished", de: "nicht fertig" },
  "config.templateRow.complete": { en: "complete", de: "vollständig" },
  "config.templateRow.close": { en: "done — collapse", de: "fertig — zuklappen" },
  "config.templateRow.remove": { en: "Remove template {n}", de: "Template {n} entfernen" },
  "config.templateRow.deliveredAfter": { en: "delivered after", de: "ausgeliefert nach" },
  "config.templateRow.builtAfter": { en: "built after", de: "gebaut nach" },
  "config.templateRow.defaultBranch": { en: "@ its default branch", de: "@ seinem Standard-Branch" },
  "config.templateRow.noEnvironment": { en: "no environment yet", de: "noch keine Umgebung" },

  // --- project sandbox: cpu and memory ---
  "config.resources.cpuRequest": { en: "cpu request", de: "CPU-Anforderung" },
  "config.resources.cpuLimit": { en: "cpu limit", de: "CPU-Limit" },
  "config.resources.memoryRequest": { en: "memory request", de: "Speicher-Anforderung" },
  "config.resources.memoryLimit": { en: "memory limit", de: "Speicher-Limit" },
  "config.resources.layer.projectOverride": { en: "this project's own block answers", de: "der eigene Block dieses Projekts gilt" },
  "config.resources.layer.lightProfile": {
    en: "a run of this pipeline changes no code, so the fixed light profile answers — the process-wide default never reaches it",
    de: "ein Run dieser Pipeline ändert keinen Code, also gilt das feste leichte Profil — der prozessweite Standard erreicht ihn nie",
  },
  "config.resources.layer.contextDocument": {
    en: "a run that changes code gets the resources the repository's context document names, clamped to the operator ceiling",
    de: "ein Run, der Code ändert, bekommt die Ressourcen aus dem Kontextdokument des Repositorys, begrenzt auf die Obergrenze des Betreibers",
  },
  "config.resources.layer.globalDefault": {
    en: "a run that changes code gets the process-wide sandbox default, unless the repository's context document names resources for that run",
    de: "ein Run, der Code ändert, bekommt den prozessweiten Sandbox-Standard, außer das Kontextdokument des Repositorys nennt Ressourcen für diesen Run",
  },
  "config.resources.layer.own": { en: "{sentence} ({values})", de: "{sentence} ({values})" },
  "config.resources.layer.withNoCode": {
    en: "{own}; a run whose pipeline changes no code is held to the fixed light profile ({values}) — each run's own pipeline decides which applies",
    de: "{own}; ein Run, dessen Pipeline keinen Code ändert, bleibt beim festen leichten Profil ({values}) — die Pipeline jedes Runs entscheidet, was gilt",
  },
  "config.resources.summary": {
    en: "cpu {cpuRequest}/{cpuLimit}, memory {memoryRequest}/{memoryLimit}",
    de: "CPU {cpuRequest}/{cpuLimit}, Speicher {memoryRequest}/{memoryLimit}",
  },
  "config.resources.label": { en: "cpu & memory", de: "CPU & Speicher" },
  "config.resources.help": {
    en: "all four quantities or none — a partial override is not a shape this product has",
    de: "alle vier Werte oder keiner — eine teilweise Überschreibung gibt es in diesem Produkt nicht",
  },
  "config.resources.layerUnreadable": {
    en: "the layer that would answer could not be read",
    de: "die Ebene, die gelten würde, ließ sich nicht lesen",
  },
  "config.resources.cannotSize": {
    en: "cannot size this project while the inherited values are unreadable",
    de: "dieses Projekt lässt sich nicht dimensionieren, solange die geerbten Werte nicht lesbar sind",
  },
  "config.resources.sizeMyself": { en: "+ size this project myself", de: "+ dieses Projekt selbst dimensionieren" },
  "config.resources.inherit": {
    en: "× hand the whole group back to what it inherits",
    de: "× die ganze Gruppe wieder auf die geerbten Werte setzen",
  },

  // --- the change history ---
  "config.changes.loading": { en: "Loading changes…", de: "Änderungen werden geladen…" },
  "config.changes.surface": { en: "the change history", de: "den Änderungsverlauf" },
  "config.changes.empty": { en: "No changes recorded yet.", de: "Noch keine Änderungen aufgezeichnet." },
  "config.changes.by": { en: "by", de: "von" },
  "config.changes.reverted": { en: "reverted", de: "zurückgesetzt" },
  "config.changes.reverting": { en: "reverting…", de: "wird zurückgesetzt…" },
  "config.changes.revert": { en: "revert ↺", de: "zurücksetzen ↺" },

  // --- repositories: what discovery found ---
  "config.inventory.empty": {
    en: "No connections configured — discovered inventory appears per connection.",
    de: "Keine Verbindungen eingerichtet — gefundene Repositories erscheinen pro Verbindung.",
  },
  "config.inventory.discoveredAt": { en: "discovered {at}", de: "erkundet {at}" },
  "config.inventory.cache": { en: "discovery cache", de: "Erkundungs-Cache" },
  "config.inventory.discovery": { en: "discovery", de: "Erkundung" },
  "config.inventory.unavailable": { en: "unavailable: {message}", de: "nicht verfügbar: {message}" },
  "config.inventory.loading": { en: "loading…", de: "wird geladen…" },
  "config.inventory.notYet": {
    en: "not discovered yet — Refresh now, or type a name on the project",
    de: "noch nicht erkundet — Jetzt aktualisieren oder am Projekt einen Namen eingeben",
  },
  "config.inventory.none": { en: "ran, but found no repos in this connection", de: "lief, fand aber keine Repos in dieser Verbindung" },
  "config.inventory.referencedBy": { en: "referenced by {project}", de: "verwendet von {project}" },

  // --- the key/value map editor ---
  "config.map.help": { en: "one key and one value per row", de: "ein Key und ein Wert pro Zeile" },
  "config.map.keyAria": { en: "{label} key {n}", de: "{label} Key {n}" },
  "config.map.valueAria": { en: "{label} value {n}", de: "{label} Wert {n}" },
  "config.map.none": { en: "no entries", de: "keine Einträge" },
  "config.map.add": { en: "+ add entry", de: "+ Eintrag hinzufügen" },

  // --- agent: the activities a model is picked for ---
  "config.activity.primary": { en: "Coding", de: "Coding" },
  "config.activity.primary.covers": { en: "master loop, sub-agents, docs, gate retries", de: "Master-Schleife, Sub-Agents, Doku, Gate-Wiederholungen" },
  "config.activity.planning": { en: "Planning", de: "Planung" },
  "config.activity.planning.covers": { en: "spec derivation, repository scope", de: "Spec-Ableitung, Repository-Umfang" },
  "config.activity.reasoning": { en: "Review & checks", de: "Review & Prüfungen" },
  "config.activity.reasoning.covers": {
    en: "diff review, premise check, spec cut, finding refutation",
    de: "Diff-Review, Prämissen-Check, Spec-Schnitt, Widerlegung von Findings",
  },
  "config.activity.contextGeneration": { en: "Project init", de: "Projekt-Init" },
  "config.activity.contextGeneration.covers": { en: "component discovery, context files", de: "Komponenten-Erkundung, Kontextdateien" },
  "config.activity.codeMapGeneration": { en: "Code map", de: "Code-Map" },
  "config.activity.codeMapGeneration.covers": { en: "codebase map for analysis", de: "Karte der Codebasis für die Analyse" },
  "config.activity.scout": { en: "Quick lookups", de: "Schnelle Abfragen" },
  "config.activity.scout.covers": {
    en: "client surface, registry auth, bootstrap docs",
    de: "Client-Oberfläche, Registry-Anmeldung, Bootstrap-Doku",
  },
  "config.activity.summarization": { en: "Summaries", de: "Zusammenfassungen" },
  "config.activity.summarization.covers": {
    en: "knowledge compile, dialogue subjects",
    de: "Wissen zusammenstellen, Dialog-Betreffzeilen",
  },
  "config.activities.noTier": { en: "no tier", de: "keine Stufe" },
  "config.activities.intro": {
    en: "Each activity picks a model from the catalog. Unset, it follows Coding.",
    de: "Jede Aktivität wählt ein Modell aus dem Katalog. Ohne Wahl folgt sie Coding.",
  },
  "config.activities.needsStrong": { en: "needs strong", de: "braucht starkes" },
  "config.activities.needsStrongMeans": {
    en: "marks an activity whose answer every later step builds on.",
    de: "markiert eine Aktivität, auf deren Antwort jeder spätere Schritt aufbaut.",
  },
  "config.activities.empty": {
    en: "The catalog is empty — add a model on the Models tab, then pick it for Coding.",
    de: "Der Katalog ist leer — füg im Tab Modelle ein Modell hinzu und wähl es dann für Coding.",
  },
  "config.activities.follows": { en: "follows {activity}", de: "folgt {activity}" },
  "config.activities.pickModel": { en: "— pick a model —", de: "— Modell wählen —" },
  "config.activities.notInCatalog": { en: "{entry} · not in the catalog", de: "{entry} · nicht im Katalog" },
  "config.activities.nothingYet": { en: "nothing yet", de: "noch nichts" },
  "config.activities.required": {
    en: "required — every other activity falls back to it",
    de: "Pflicht — jede andere Aktivität fällt darauf zurück",
  },
  "config.activities.undeclared": {
    en: "names an entry the catalog does not declare",
    de: "nennt einen Eintrag, den der Katalog nicht deklariert",
  },
  "config.activities.mismatch": {
    en: "▲ a fast model on an activity that needs a strong one",
    de: "▲ ein schnelles Modell für eine Aktivität, die ein starkes braucht",
  },

  // --- agent: prices and windows ---
  "config.price.source.override": { en: "override", de: "Überschreibung" },
  "config.price.source.list": { en: "price list", de: "Preisliste" },
  "config.price.source.none": { en: "not in list", de: "nicht in der Liste" },
  "config.price.none": { en: "no price", de: "kein Preis" },
  "config.window.none": { en: "no window stated", de: "kein Kontextfenster angegeben" },
  "config.window.size": { en: "{size} window", de: "{size} Kontextfenster" },

  // --- agent: the Models tab ---
  "config.models.source": { en: "Prices & windows from the public price list ·", de: "Preise & Kontextfenster aus der öffentlichen Preisliste ·" },
  "config.models.pricesFailed": {
    en: "The public price list could not be read — prices show once it loads.",
    de: "Die öffentliche Preisliste ließ sich nicht lesen — Preise erscheinen, sobald sie geladen ist.",
  },
  "config.models.pricesLoading": { en: "Loading the public price list…", de: "Öffentliche Preisliste wird geladen…" },
  "config.models.add": { en: "+ Add model", de: "+ Modell hinzufügen" },
  "config.models.unpriced": {
    en: {
      one: "matches nothing in the price list — the save is refused until you pick a listed model or enter a price.",
      other: "match nothing in the price list — the save is refused until you pick a listed model or enter a price.",
    },
    de: {
      one: "steht nicht in der Preisliste — Speichern wird abgelehnt, bis du ein gelistetes Modell wählst oder einen Preis einträgst.",
      other: "stehen nicht in der Preisliste — Speichern wird abgelehnt, bis du ein gelistetes Modell wählst oder einen Preis einträgst.",
    },
  },
  "config.models.empty": {
    en: "No models yet. Add one, then pick it for Coding on the Activities tab.",
    de: "Noch keine Modelle. Füg eins hinzu und wähl es dann im Tab Aktivitäten für Coding.",
  },
  "config.models.editAria": { en: "Edit model {name}", de: "Modell {name} bearbeiten" },
  "config.models.unpricedMark": { en: "▲ unpriced", de: "▲ ohne Preis" },
  "config.models.noModelId": { en: "no model id", de: "keine Modell-ID" },
  "config.models.usedBy": { en: "used by {activities}", de: "genutzt von {activities}" },
  "config.models.notAssigned": { en: "not assigned", de: "nicht zugewiesen" },
  "config.models.orphans.title": { en: "Price overrides for models not in the catalog", de: "Preis-Überschreibungen für Modelle außerhalb des Katalogs" },
  "config.models.orphans.help": { en: "kept on save until removed", de: "bleiben beim Speichern erhalten, bis du sie entfernst" },
  "config.models.orphans.removeAria": { en: "Remove price override for {model}", de: "Preis-Überschreibung für {model} entfernen" },

  // --- agent: adding or editing one model ---
  "config.modelEditor.tier.none": { en: "none", de: "keine" },
  "config.modelEditor.back": { en: "Back to models", de: "Zurück zu den Modellen" },
  "config.modelEditor.crumb": { en: "Models /", de: "Modelle /" },
  "config.modelEditor.addTitle": { en: "Add model", de: "Modell hinzufügen" },
  "config.modelEditor.deployment.label": { en: "deployment", de: "Deployment" },
  "config.modelEditor.deployment.help": {
    en: "the Azure deployment name, when it differs from the model id",
    de: "der Azure-Deployment-Name, wenn er von der Modell-ID abweicht",
  },
  "config.modelEditor.priceWindow": { en: "Price & window", de: "Preis & Kontextfenster" },
  "config.modelEditor.charges": {
    en: "What the resolver charges per million tokens (in / out / cache read):",
    de: "Was der Resolver pro Million Tokens berechnet (Input / Output / Cache-Read):",
  },
  "config.modelEditor.unknownModel": {
    en: "The price list does not know this model — enter an override or the save is refused.",
    de: "Die Preisliste kennt dieses Modell nicht — trag eine Überschreibung ein, sonst wird Speichern abgelehnt.",
  },
  "config.modelEditor.override": {
    en: "Override — this installation pays a different rate",
    de: "Überschreiben — diese Installation zahlt einen anderen Preis",
  },
  "config.modelEditor.inputPerM": { en: "input / M", de: "Input / M" },
  "config.modelEditor.outputPerM": { en: "output / M", de: "Output / M" },
  "config.modelEditor.cacheReadPerM": { en: "cache read / M", de: "Cache-Read / M" },
  "config.modelEditor.window.label": { en: "context window", de: "Kontextfenster" },
  "config.modelEditor.window.help": {
    en: "input tokens the deployment accepts; compaction derives its threshold from it",
    de: "Input-Tokens, die das Deployment annimmt; die Compaction leitet ihre Schwelle davon ab",
  },
  "config.modelEditor.window.useList": { en: "use list value ({value})", de: "Listenwert übernehmen ({value})" },
  "config.modelEditor.window.fromList": { en: "from price list", de: "aus der Preisliste" },
  "config.modelEditor.maxTokens.label": { en: "max tokens", de: "Max. Tokens" },
  "config.modelEditor.maxTokens.help": { en: "per-call output cap; blank = 8192", de: "Output-Obergrenze pro Aufruf; leer = 8192" },
  "config.modelEditor.api.label": { en: "api", de: "API" },
  "config.modelEditor.api.help": {
    en: "responses for a reasoning model that calls tools (GPT-6); blank = chat_completions",
    de: "responses für ein Reasoning-Modell, das Tools aufruft (GPT-6); leer = chat_completions",
  },
  "config.modelEditor.api.placeholder": { en: "chat_completions", de: "chat_completions" },
  "config.modelEditor.effort.label": { en: "reasoning effort", de: "Reasoning-Aufwand" },
  "config.modelEditor.effort.help": {
    en: "sent as written: none, low, medium, high, xhigh or max, as the model takes them; blank = the model's default",
    de: "wird so gesendet: none, low, medium, high, xhigh oder max, je nachdem, was das Modell nimmt; leer = Standard des Modells",
  },
  "config.modelEditor.tier.label": { en: "tier", de: "Stufe" },
  "config.modelEditor.tier.help": {
    en: "an activity that needs a strong model warns when a fast one is picked",
    de: "eine Aktivität, die ein starkes Modell braucht, warnt, wenn ein schnelles gewählt ist",
  },
  "config.modelEditor.name.label": { en: "name in the catalog", de: "Name im Katalog" },
  "config.modelEditor.name.taken": { en: "another entry already has this name", de: "ein anderer Eintrag hat diesen Namen schon" },
  "config.modelEditor.name.help": { en: "what an activity picks", de: "was eine Aktivität auswählt" },
  "config.modelEditor.connection.title": { en: "Different provider or endpoint", de: "Anderer Provider oder Endpoint" },
  "config.modelEditor.connection.set": { en: "set", de: "gesetzt" },
  "config.modelEditor.connection.own": { en: "the agent's own", de: "der des Agents" },
  "config.modelEditor.connection.providerPlaceholder": { en: "— the agent's provider —", de: "— der Provider des Agents —" },
  "config.modelEditor.connection.endpointHelp": { en: "blank uses the agent's", de: "leer nutzt den des Agents" },
  "config.modelEditor.remove": { en: "Remove from catalog", de: "Aus dem Katalog entfernen" },
  "config.modelEditor.apply": { en: "Apply", de: "Übernehmen" },
  "config.modelEditor.addToCatalog": { en: "Add to catalog", de: "Zum Katalog hinzufügen" },
  "config.modelEditor.removeRefused": {
    en: {
      one: "Cannot remove: {activities} uses this model. Pick another model for it on the Activities tab first.",
      other: "Cannot remove: {activities} use this model. Pick another model for them on the Activities tab first.",
    },
    de: {
      one: "Entfernen nicht möglich: {activities} nutzt dieses Modell. Wähl dafür zuerst im Tab Aktivitäten ein anderes Modell.",
      other: "Entfernen nicht möglich: {activities} nutzen dieses Modell. Wähl dafür zuerst im Tab Aktivitäten ein anderes Modell.",
    },
  },
  "config.modelEditor.invalid.nameTaken": { en: "Pick a name no other entry has.", de: "Wähl einen Namen, den kein anderer Eintrag hat." },
  "config.modelEditor.invalid.noName": { en: "Give the entry a name.", de: "Gib dem Eintrag einen Namen." },
  "config.modelEditor.invalid.noModel": { en: "Pick the model it runs.", de: "Wähl das Modell, das läuft." },

  // --- agent: the Tuning tab ---
  "config.tuning.notSet": { en: "not set", de: "nicht gesetzt" },
  "config.tuning.clear": { en: "Remove section (use backend default)", de: "Abschnitt entfernen (Standard des Backends nutzen)" },
  "config.tuning.cache": { en: "Cache", de: "Cache" },
  "config.tuning.cache.none": { en: "No cache settings — the backend default applies.", de: "Keine Cache-Einstellungen — der Standard des Backends gilt." },
  "config.tuning.cache.add": { en: "Add cache settings", de: "Cache-Einstellungen hinzufügen" },
  "config.tuning.cache.prompt": { en: "prompt caching", de: "Prompt-Caching" },
  "config.tuning.cache.strategy": { en: "strategy", de: "Strategie" },
  "config.tuning.compaction": { en: "Compaction", de: "Compaction" },
  "config.tuning.compaction.none": {
    en: "No compaction settings — the backend default applies.",
    de: "Keine Compaction-Einstellungen — der Standard des Backends gilt.",
  },
  "config.tuning.compaction.add": { en: "Add compaction settings", de: "Compaction-Einstellungen hinzufügen" },
  "config.tuning.compaction.enabled": { en: "compaction", de: "Compaction" },
  "config.tuning.compaction.threshold": { en: "threshold iterations", de: "Schwelle (Iterationen)" },
  "config.tuning.compaction.maxContext": { en: "max context tokens", de: "Max. Kontext-Tokens" },
  "config.tuning.compaction.keepRecent": { en: "keep recent iterations", de: "letzte Iterationen behalten" },
  "config.tuning.compaction.trigger.label": { en: "trigger ratio", de: "Auslöse-Anteil" },
  "config.tuning.compaction.trigger.help": {
    en: "fold once the context reaches this share of max context tokens (default 0.7)",
    de: "zusammenfalten, sobald der Kontext diesen Anteil der max. Kontext-Tokens erreicht (Standard 0.7)",
  },
  "config.tuning.compaction.foldTarget.label": { en: "fold target ratio", de: "Ziel-Anteil nach dem Falten" },
  "config.tuning.compaction.foldTarget.help": {
    en: "a fold shrinks the view to this share of max context tokens (default 0.35)",
    de: "ein Falten verkleinert die Sicht auf diesen Anteil der max. Kontext-Tokens (Standard 0.35)",
  },
  "config.tuning.compaction.summaryBudget.label": { en: "summary budget ratio", de: "Budget-Anteil der Zusammenfassung" },
  "config.tuning.compaction.summaryBudget.help": {
    en: "past this share the summary segments are merged (default 0.1)",
    de: "über diesem Anteil werden die Zusammenfassungs-Segmente zusammengeführt (Standard 0.1)",
  },
  "config.tuning.retry": { en: "Retry", de: "Wiederholung" },
  "config.tuning.retry.count": {
    en: { one: "1 retry", other: "{count} retries" },
    de: { one: "1 Wiederholung", other: "{count} Wiederholungen" },
  },
  "config.tuning.retry.none": { en: "No retry policy — the backend default applies.", de: "Keine Wiederholungsregel — der Standard des Backends gilt." },
  "config.tuning.retry.add": { en: "Add retry policy", de: "Wiederholungsregel hinzufügen" },
  "config.tuning.retry.max": { en: "max retries", de: "Max. Wiederholungen" },
  "config.tuning.retry.initialDelay": { en: "initial delay (ms)", de: "erste Wartezeit (ms)" },
  "config.tuning.retry.backoff": { en: "backoff multiplier", de: "Backoff-Faktor" },
  "config.tuning.retry.maxDelay": { en: "max delay (ms)", de: "max. Wartezeit (ms)" },

  // --- the entity form ---
  "config.field.commaSeparated": { en: "{label} (comma separated)", de: "{label} (durch Komma getrennt)" },
  "config.form.id": { en: "{kind} id", de: "{kind}-ID" },
  "config.form.idPlaceholder": { en: "unique-id", de: "eindeutige-id" },
  "config.form.name": { en: "name", de: "Name" },
  "config.form.branch": { en: "branch", de: "Branch" },
  "config.form.transport": { en: "transport", de: "Transport" },
  "config.form.url": { en: "url", de: "URL" },
  "config.form.authSecret": { en: "auth secret", de: "Auth-Secret" },
  "config.form.vendor": { en: "vendor", de: "Anbieter" },
  "config.form.displayName": { en: "display name", de: "Anzeigename" },
  "config.form.secret.redacted": { en: "value redacted", de: "Wert verborgen" },
  "config.form.secret.redactedNote": {
    en: "A value set here is stored encrypted and never shown again; an environment variable of the same name wins.",
    de: "Ein hier gesetzter Wert wird verschlüsselt gespeichert und nie wieder angezeigt; eine gleichnamige Umgebungsvariable hat Vorrang.",
  },

  // --- project: the sandbox tab ---
  "config.sandbox.provenance.unavailable": { en: "inherited value unavailable", de: "geerbter Wert nicht verfügbar" },
  "config.sandbox.provenance.runResolved": { en: "detected per run from the repository", de: "pro Run aus dem Repository ermittelt" },
  "config.sandbox.provenance.nothing": { en: "nothing to inherit — none is configured", de: "nichts zu erben — nichts eingerichtet" },
  "config.sandbox.provenance.environment": {
    en: "inherits {value} from the SANDBOX_HOLD_SECONDS environment variable",
    de: "erbt {value} aus der Umgebungsvariable SANDBOX_HOLD_SECONDS",
  },
  "config.sandbox.provenance.builtIn": { en: "inherits the built-in {value}", de: "erbt den eingebauten Wert {value}" },
  "config.sandbox.provenance.inherits": { en: "inherits {value}", de: "erbt {value}" },
  "config.sandbox.inheritanceNote": {
    en: "Every field here is an override. Leave one empty and the project inherits what the placeholder names; applies to the next run of this project. The process-wide values it falls back to — the hold window excepted, which is read live — are read once at startup, so changing THOSE in Settings → sandbox needs a server restart.",
    de: "Jedes Feld hier überschreibt. Lässt du eins leer, erbt das Projekt, was der Platzhalter nennt; gilt ab dem nächsten Run dieses Projekts. Die prozessweiten Werte, auf die es zurückfällt — außer dem Halte-Fenster, das live gelesen wird —, werden einmal beim Start gelesen; sie in Einstellungen → Sandbox zu ändern braucht also einen Neustart des Servers.",
  },
  "config.sandbox.inheritedUnavailable": {
    en: "The inherited values could not be read, so the placeholders are missing. An empty field still means “inherit”.",
    de: "Die geerbten Werte ließen sich nicht lesen, daher fehlen die Platzhalter. Ein leeres Feld heißt trotzdem „erben“.",
  },
  "config.sandbox.draftNote": {
    en: "This project is not in the running configuration yet, so it has no resolved row — the values named below are the process-wide ones it will inherit once it is saved.",
    de: "Dieses Projekt ist noch nicht in der laufenden Konfiguration, hat also keine aufgelöste Zeile — die Werte unten sind die prozessweiten, die es nach dem Speichern erbt.",
  },
  "config.sandbox.toolchainImage.label": { en: "toolchain image", de: "Toolchain-Image" },
  "config.sandbox.toolchainImage.help": { en: "whole-project sandbox image — {provenance}", de: "Sandbox-Image für das ganze Projekt — {provenance}" },
  "config.sandbox.stepTimeout.label": { en: "step timeout (seconds)", de: "Schritt-Timeout (Sekunden)" },
  "config.sandbox.stepTimeout.help": {
    en: "per-step wall-time cap for this project — {provenance}",
    de: "Laufzeit-Obergrenze pro Schritt für dieses Projekt — {provenance}",
  },
  "config.sandbox.runCommandTimeout.label": { en: "run_command timeout (seconds)", de: "run_command-Timeout (Sekunden)" },
  "config.sandbox.runCommandTimeout.help": {
    en: "what a run_command gets when it asks for none — {provenance}",
    de: "was ein run_command bekommt, wenn es nichts verlangt — {provenance}",
  },
  "config.sandbox.agentRegistry.label": { en: "agent registry", de: "Agent-Registry" },
  "config.sandbox.agentRegistry.help": {
    en: "registry this project pulls the sandbox agent from — {provenance}",
    de: "Registry, aus der dieses Projekt den Sandbox-Agent zieht — {provenance}",
  },
  "config.sandbox.agentVersion.label": { en: "agent version", de: "Agent-Version" },
  "config.sandbox.agentVersion.help": {
    en: "sandbox agent image tag for this project — {provenance}",
    de: "Image-Tag des Sandbox-Agents für dieses Projekt — {provenance}",
  },
  "config.sandbox.hold.label": { en: "Sandbox hold (seconds)", de: "Sandbox halten (Sekunden)" },
  "config.sandbox.hold.help": {
    en: "how long this project's design conversations hold their source sandboxes between turns — {provenance}; applies to the next turn of a design conversation and reaches a reaper scan without a server restart; 0 holds nothing",
    de: "wie lange die Design-Gespräche dieses Projekts ihre Quell-Sandboxes zwischen den Turns halten — {provenance}; gilt ab dem nächsten Turn eines Design-Gesprächs und erreicht einen Reaper-Durchlauf ohne Neustart des Servers; 0 hält nichts",
  },
  "config.sandbox.browser.label": { en: "browser rendering in runs", de: "Browser-Rendering in Runs" },
  "config.sandbox.browser.note": {
    en: "On, a run's coding master can render the uploaded websites its approval cites and public pages with render_reference, and each run of this project reserves one browser pod at the process-wide browser profile; applies to the next run of this project.",
    de: "An: der Coding-Master eines Runs kann mit render_reference die hochgeladenen Websites rendern, die seine Freigabe zitiert, und öffentliche Seiten; jeder Run dieses Projekts reserviert einen Browser-Pod mit dem prozessweiten Browser-Profil; gilt ab dem nächsten Run dieses Projekts.",
  },
  "config.sandbox.root.label": { en: "run sandboxes as root", de: "Sandboxes als root ausführen" },
  "config.sandbox.root.note": {
    en: "Matters only when the installation hardens sandboxes (Settings, Sandbox). Then sandboxes run as uid 1000 with no Linux capabilities, and this turns root back on for this project when a prerequisite needs it, such as apt-get or a global npm install. Every run of this project then shows a warning; applies to the next run of this project.",
    de: "Zählt nur, wenn die Installation Sandboxes härtet (Einstellungen, Sandbox). Dann laufen Sandboxes als uid 1000 ohne Linux-Capabilities, und das hier schaltet root für dieses Projekt wieder ein, wenn eine Voraussetzung es braucht, etwa apt-get oder ein globales npm install. Jeder Run dieses Projekts zeigt dann eine Warnung; gilt ab dem nächsten Run dieses Projekts.",
  },
  "config.sandbox.images.label": { en: "per-language images", de: "Images pro Sprache" },
  "config.sandbox.images.help": {
    en: "one language and the image it is built in — every language you do not name keeps inheriting",
    de: "eine Sprache und das Image, in dem sie gebaut wird — jede Sprache, die du nicht nennst, erbt weiter",
  },
  "config.sandbox.images.overrides": {
    en: "overrides the code default {image} for {key} only",
    de: "überschreibt den Code-Standard {image} nur für {key}",
  },
  "config.sandbox.images.pins": {
    en: "pins {key}, which the product has no code default for",
    de: "legt {key} fest, wofür das Produkt keinen Code-Standard hat",
  },
  "config.sandbox.images.noDefault": { en: "no code default for this key", de: "kein Code-Standard für diesen Key" },

  // --- the create/edit drawer ---
  "config.drawer.closeDrawer": { en: "Close drawer", de: "Seitenleiste schließen" },
  "config.drawer.label": { en: "Create or edit", de: "Anlegen oder bearbeiten" },
  "config.drawer.new": { en: "New {kind}", de: "{kind} anlegen" },
  "config.drawer.edit": { en: "Edit {kind}", de: "{kind} bearbeiten" },
  "config.drawer.close": { en: "Close", de: "Schließen" },
  "config.drawer.readyCreate": { en: "Ready to create", de: "Bereit zum Anlegen" },
  "config.drawer.readySave": { en: "Ready to save", de: "Bereit zum Speichern" },
  "config.drawer.resolveRefs": { en: "resolve all references to save", de: "lös alle Verweise auf, um zu speichern" },
  "config.drawer.templateUnfinished": {
    en: "template {n} is unfinished — give it a context, a project, a repo and a template context",
    de: "Template {n} ist nicht fertig — gib ihm einen Kontext, ein Projekt, ein Repo und einen Template-Kontext",
  },
  "config.drawer.pickType": { en: "pick a type and fill its required fields", de: "wähl einen Typ und füll seine Pflichtfelder aus" },
  "config.drawer.fillRequired": { en: "Fill the required fields", de: "Füll die Pflichtfelder aus" },
  "config.drawer.create": { en: "Create", de: "Anlegen" },
  "config.drawer.blockedOnTab": { en: "{reason} ({tab} tab)", de: "{reason} (Tab {tab})" },

  // --- the catalog page ---
  "config.kind.agents.empty": { en: "No agents yet. Create your first one.", de: "Noch keine Agents. Leg den ersten an." },
  "config.kind.trackers.empty": { en: "No trackers yet. Create your first one.", de: "Noch keine Tracker. Leg den ersten an." },
  "config.kind.connections.empty": { en: "No connections yet. Create your first one.", de: "Noch keine Verbindungen. Leg die erste an." },
  "config.kind.repos.empty": { en: "No repositories yet. Create your first one.", de: "Noch keine Repositories. Leg das erste an." },
  "config.kind.projects.empty": { en: "No projects yet. Create your first one.", de: "Noch keine Projekte. Leg das erste an." },
  "config.kind.mcpServers.empty": { en: "No mcp servers yet. Create your first one.", de: "Noch keine MCP-Server. Leg den ersten an." },
  "config.kind.designSources.empty": { en: "No design sources yet. Create your first one.", de: "Noch keine Design-Quellen. Leg die erste an." },
  "config.kind.secrets.empty": { en: "No secrets yet. Create your first one.", de: "Noch keine Secrets. Leg das erste an." },
  "config.changes.sub": {
    en: "Every config edit — who, when, what — revertible. The audit trail a hand-edited config map never had.",
    de: "Jede Änderung an der Konfiguration — wer, wann, was — umkehrbar. Der Prüfpfad, den eine von Hand gepflegte Config nie hatte.",
  },
  "config.catalog.surface": { en: "the configuration catalog", de: "den Konfigurationskatalog" },
  "config.catalog.loadFailed": {
    en: "Failed to load configuration: {message}",
    de: "Die Konfiguration ließ sich nicht laden: {message}",
  },
  "config.thesis.text": {
    en: "The source of truth is this catalog — refs are picked, never typed, so a project can’t point at an agent that doesn’t exist.",
    de: "Die Quelle der Wahrheit ist dieser Katalog — Verweise werden ausgewählt, nie getippt, also kann ein Projekt nicht auf einen Agent zeigen, den es nicht gibt.",
  },
  "config.thesis.bold": { en: "No hand-edited config map.", de: "Keine von Hand gepflegte Config." },
  "config.import.done": { en: "Imported {count} config entities.", de: "{count} Konfigurationseinträge importiert." },
  "config.import.doneWithDropped": {
    en: "Imported {count} config entities. Not imported: {dropped}.",
    de: "{count} Konfigurationseinträge importiert. Nicht importiert: {dropped}.",
  },
  "config.import.overwriteQuestion": {
    en: "Overwrite the current config?\n\n{message}",
    de: "Die aktuelle Konfiguration überschreiben?\n\n{message}",
  },
  "config.import.notEmpty": { en: "Config store is not empty.", de: "Der Config-Speicher ist nicht leer." },
  "config.import.overwrite": { en: "Overwrite", de: "Überschreiben" },
  "config.import.keep": { en: "Keep current", de: "Aktuelle behalten" },
  "config.import.failed": { en: "import failed: {message}", de: "Import fehlgeschlagen: {message}" },
  "config.import.running": { en: "Importing…", de: "Wird importiert…" },
  "config.import.button": { en: "Import agentsmith.yml ↥", de: "agentsmith.yml importieren ↥" },
  "config.export.failed": { en: "export failed: {message}", de: "Export fehlgeschlagen: {message}" },
  "config.export.running": { en: "Exporting…", de: "Wird exportiert…" },
  "config.export.button": { en: "Export agentsmith.yml ↧", de: "agentsmith.yml exportieren ↧" },
  "config.repos.discovered.title": { en: "Discovered per connection", de: "Pro Verbindung gefunden" },
  "config.repos.discovered.sub": {
    en: "what the discovery cache actually found — wire these as conn/Name",
    de: "was der Erkundungs-Cache tatsächlich gefunden hat — verdrahte sie als conn/Name",
  },
  "config.repos.standalone.title": { en: "Standalone catalog", de: "Eigenständiger Katalog" },
  "config.repos.standalone.sub": {
    en: "individually registered repos — the legacy world",
    de: "einzeln registrierte Repos — die Altwelt",
  },

  // --- the project form ---
  "config.none": { en: "— none —", de: "— keine —" },
  "config.project.sections": { en: "Project sections", de: "Projekt-Bereiche" },
  "config.project.tab.identity": { en: "identity", de: "Identität" },
  "config.project.tab.repos": { en: "repos", de: "Repos" },
  "config.project.tab.pipeline": { en: "pipeline", de: "Pipeline" },
  "config.project.tab.templates": { en: "templates", de: "Templates" },
  "config.project.tab.routing": { en: "routing", de: "Routing" },
  "config.project.tab.sandbox": { en: "sandbox", de: "Sandbox" },
  "config.project.tab.merge": { en: "merge", de: "Merge" },
  "config.project.tab.machine": { en: "machine identity", de: "Maschinen-Identität" },
  "config.project.agent": { en: "agent", de: "Agent" },
  "config.project.tracker": { en: "tracker", de: "Tracker" },
  "config.project.designSources": { en: "design sources", de: "Design-Quellen" },
  "config.project.repos": { en: "repos", de: "Repos" },
  "config.project.connectionRepos": { en: "connection-scoped repos", de: "Repos aus Verbindungen" },
  "config.project.defaultPipeline": { en: "default pipeline", de: "Standard-Pipeline" },
  "config.project.pipeline.retired": {
    en: "'{value}' is a retired name — it no longer runs; pick a current one to replace it",
    de: "'{value}' ist ein ausgemusterter Name — er läuft nicht mehr; wähl einen aktuellen als Ersatz",
  },
  "config.project.pipeline.help": {
    en: "which pipeline definition this project runs; what a TICKET routes to is the tracker's, below",
    de: "welche Pipeline-Definition dieses Projekt ausführt; wohin ein TICKET geleitet wird, entscheidet der Tracker, unten",
  },
  "config.project.findings.label": { en: "what the server would report", de: "was der Server melden würde" },
  "config.project.findings.help": { en: "before you save", de: "bevor du speicherst" },
  "config.project.verdict.oneRepo": { en: "at least one repo", de: "mindestens ein Repo" },
  "config.project.verdict.unknownRepo": { en: "unknown repo/connection {refs}", de: "unbekanntes Repo/unbekannte Verbindung {refs}" },
  "config.project.verdict.ok": {
    en: "Every reference resolves to a catalog entry — this project will pass config validation.",
    de: "Jeder Verweis zeigt auf einen Katalogeintrag — dieses Projekt besteht die Konfigurationsprüfung.",
  },
  "config.project.verdict.unresolved": { en: "Unresolved: {missing}", de: "Nicht aufgelöst: {missing}" },
  "config.resolution.strategy.label": { en: "resolution strategy", de: "Zuordnungsstrategie" },
  "config.resolution.strategy.help": { en: "how a ticket resolves to this project", de: "wie ein Ticket diesem Projekt zugeordnet wird" },
  "config.resolution.value.label": { en: "resolution value", de: "Zuordnungswert" },
  "config.resolution.value.placeholder": { en: "match value for this strategy", de: "Vergleichswert für diese Strategie" },
  "config.resolution.value.help": { en: "value the {strategy} strategy matches on", de: "Wert, mit dem die Strategie {strategy} vergleicht" },
  "config.resolution.tag.placeholder": { en: "e.g. checkout", de: "z. B. checkout" },
  "config.resolution.tag.help": { en: "ticket tag/label that routes to this project", de: "Ticket-Tag/-Label, das zu diesem Projekt leitet" },
  "config.resolution.areaPath.placeholder": { en: "e.g. Product\\Team\\Component", de: "z. B. Product\\Team\\Component" },
  "config.resolution.areaPath.help": { en: "the ticket's area path", de: "der Area Path des Tickets" },
  "config.resolution.repo.placeholder": { en: "e.g. Sample.Api", de: "z. B. Sample.Api" },
  "config.resolution.repo.help": { en: "a repo name mentioned on the ticket", de: "ein Repo-Name, der im Ticket steht" },
  "config.resolution.toAddress.placeholder": { en: "e.g. team@example.com", de: "z. B. team@example.com" },
  "config.resolution.toAddress.help": { en: "the inbound email address", de: "die eingehende E-Mail-Adresse" },

  // --- project: connection-scoped repo picker ---
  "config.repoPicker.connection": { en: "connection", de: "Verbindung" },
  "config.repoPicker.discovered": { en: "discovered repos", de: "gefundene Repos" },
  "config.repoPicker.count": {
    en: "showing {shown} of {matched} matched · {discovered} discovered",
    de: "zeige {shown} von {matched} Treffern · {discovered} gefunden",
  },
  "config.repoPicker.hidden": { en: " · {hidden} hidden", de: " · {hidden} ausgeblendet" },
  "config.repoPicker.selectAllRule": { en: "select all {count} as rule {rule}", de: "alle {count} als Regel {rule} wählen" },
  "config.repoPicker.selectAll": { en: "select all {count}", de: "alle {count} wählen" },
  "config.repoPicker.noMatch": { en: "no discovered repo matches “{query}”", de: "kein gefundenes Repo passt zu „{query}“" },
  "config.repoPicker.more": { en: "show {count} more", de: "{count} weitere zeigen" },
  "config.repoPicker.add": { en: "add {ref}", de: "{ref} hinzufügen" },
  "config.repoPicker.addRule": { en: "add rule {ref}", de: "Regel {ref} hinzufügen" },
  "config.repoPicker.addRuleCounted": { en: "add rule {ref} · {count} match", de: "Regel {ref} hinzufügen · {count} Treffer" },
  "config.repoPicker.noneSelected": { en: "no connection-scoped repos", de: "keine Repos aus Verbindungen" },
  "config.repoPicker.match": { en: "{count} match", de: "{count} Treffer" },
  "config.repoPicker.removeAria": { en: "Remove {ref}", de: "{ref} entfernen" },
  "config.repoPicker.loading": { en: "loading discovery cache…", de: "Erkundungs-Cache wird geladen…" },
  "config.repoPicker.unavailable": { en: "discovery cache unavailable: {message}", de: "Erkundungs-Cache nicht verfügbar: {message}" },
  "config.repoPicker.notYet": {
    en: "not discovered yet — Refresh now or type a name below",
    de: "noch nicht erkundet — Jetzt aktualisieren oder unten einen Namen eingeben",
  },
  "config.repoPicker.none": {
    en: "discovery ran but found no repos in this connection",
    de: "die Erkundung lief, fand aber keine Repos in dieser Verbindung",
  },
  "config.repoPicker.coveredBy": { en: "covered by {rule}", de: "abgedeckt durch {rule}" },
  "config.repoPicker.filterAria": { en: "filter discovered repos", de: "gefundene Repos filtern" },
  "config.repoPicker.filterPlaceholder": { en: "filter — or a wildcard, e.g. Sample.*", de: "filtern — oder ein Platzhalter, z. B. Sample.*" },
  "config.repoPicker.addButton": { en: "Add", de: "Hinzufügen" },

  // --- the entity cards ---
  "config.card.edit": { en: "Edit", de: "Bearbeiten" },
  "config.card.editHint": { en: "edit ›", de: "bearbeiten ›" },
  "config.card.envName": { en: "env-name", de: "Umgebungsname" },
  "config.card.secret": { en: "secret {secret}", de: "Secret {secret}" },
  "config.card.noKeySecret": { en: "no key secret", de: "kein Key-Secret" },
  "config.card.auth": { en: "auth {secret}", de: "Zugang {secret}" },
  "config.card.usedBy": { en: "used by {users}", de: "genutzt von {users}" },
  "config.card.notReferenced": { en: "not referenced yet", de: "noch nirgends verwendet" },
  "config.card.registry": { en: "registry {host}", de: "Registry {host}" },
  "config.card.pocTarget": { en: "PoC target {name}", de: "PoC-Ziel {name}" },
  "config.card.roleModel": { en: "{role} model", de: "Modell {role}" },
  "config.card.organisation": { en: "Organisation", de: "Organisation" },
  "config.card.project": { en: "Project", de: "Projekt" },
  "config.card.url": { en: "Url", de: "URL" },
  "config.card.authLabel": { en: "Auth", de: "Zugang" },
  "config.card.defaultBranch": { en: "Default branch", de: "Standard-Branch" },
  "config.card.transport": { en: "Transport", de: "Transport" },
  "config.card.vendor": { en: "Vendor", de: "Anbieter" },
  "config.card.noDefaultPipeline": { en: "no default pipeline", de: "keine Standard-Pipeline" },
  "config.card.defaultPipeline": { en: "default · {pipeline}", de: "Standard · {pipeline}" },
  "config.card.repositories": {
    en: { one: "1 repository", other: "{count} repositories" },
    de: { one: "1 Repository", other: "{count} Repositories" },
  },
  "config.card.builtAfter": { en: "built after {targets}", de: "gebaut nach {targets}" },
  "config.card.templatesUnfinished": {
    en: { one: "1 template unfinished", other: "{count} templates unfinished" },
    de: { one: "1 Template nicht fertig", other: "{count} Templates nicht fertig" },
  },
  "config.card.unresolved": { en: "{count} unresolved", de: "{count} nicht aufgelöst" },

  // --- the project wiring graph ---
  "config.graph.header.agent": { en: "AGENT · TRACKER", de: "AGENT · TRACKER" },
  "config.graph.header.project": { en: "PROJECT", de: "PROJEKT" },
  "config.graph.header.repositories": { en: "REPOSITORIES", de: "REPOSITORIES" },
  "config.graph.header.contexts": { en: "CONTEXTS", de: "KONTEXTE" },
  "config.graph.header.builtAfter": { en: "BUILT AFTER", de: "GEBAUT NACH" },
  "config.graph.claim": {
    en: {
      one: "How {project} is wired: its agent and tracker, its 1 repository, the contexts its own templates name, and the template each of those is built after.",
      other: "How {project} is wired: its agent and tracker, its {count} repositories, the contexts its own templates name, and the template each of those is built after.",
    },
    de: {
      one: "Wie {project} verdrahtet ist: sein Agent und Tracker, sein 1 Repository, die Kontexte, die seine eigenen Templates nennen, und das Template, nach dem jeder davon gebaut wird.",
      other: "Wie {project} verdrahtet ist: sein Agent und Tracker, seine {count} Repositories, die Kontexte, die seine eigenen Templates nennen, und das Template, nach dem jeder davon gebaut wird.",
    },
  },
  "config.graph.noAgent": { en: "no agent", de: "kein Agent" },
  "config.graph.noTracker": { en: "no tracker", de: "kein Tracker" },
  "config.graph.noContext": { en: "no context", de: "kein Kontext" },
  "config.graph.caption.agent": { en: "agent", de: "Agent" },
  "config.graph.caption.tracker": { en: "tracker", de: "Tracker" },
  "config.graph.caption.project": { en: "project", de: "Projekt" },
  "config.graph.caption.repository": { en: "repository", de: "Repository" },
  "config.graph.caption.context": { en: "context", de: "Kontext" },
  "config.graph.caption.builtAfter": { en: "built after", de: "gebaut nach" },
  "config.graph.repoNotNamed": { en: "repository not named", de: "Repository nicht genannt" },
  "config.graph.repoNotDeclared": { en: "repository not declared here", de: "Repository hier nicht deklariert" },

  // --- settings: orchestrator ---
  "config.orchestrator.wallTime.label": { en: "Max run wall-time (seconds)", de: "Max. Laufzeit eines Runs (Sekunden)" },
  "config.orchestrator.wallTime.help": {
    en: "a run older than this is cancelled by the watchdog",
    de: "ein Run, der älter ist, wird vom Watchdog abgebrochen",
  },

  // --- settings: sandbox ---
  "config.sandboxSetting.registry.label": { en: "Agent registry", de: "Agent-Registry" },
  "config.sandboxSetting.registry.help": {
    en: "the registry the sandbox agent image is pulled from — applies after a server restart",
    de: "die Registry, aus der das Image des Sandbox-Agents gezogen wird — gilt nach einem Neustart des Servers",
  },
  "config.sandboxSetting.version.label": { en: "Agent version", de: "Agent-Version" },
  "config.sandboxSetting.version.help": {
    en: "sandbox agent image tag — applies after a server restart",
    de: "Image-Tag des Sandbox-Agents — gilt nach einem Neustart des Servers",
  },
  "config.sandboxSetting.timeoutsNote": {
    en: "A run_command gets the default when it asks for no timeout, may ask for more, and is killed at the step cap — so raising the cap raises what a command may ask for.",
    de: "Ein run_command bekommt den Standard, wenn es keinen Timeout verlangt, darf mehr verlangen und wird am Schritt-Limit beendet — das Limit anzuheben hebt also an, was ein Befehl verlangen darf.",
  },
  "config.sandboxSetting.step.label": { en: "Step timeout (seconds)", de: "Schritt-Timeout (Sekunden)" },
  "config.sandboxSetting.step.help": {
    en: "per-sandbox-step wall-time cap — the ceiling a run_command may ask for — applies after a server restart",
    de: "Laufzeit-Obergrenze pro Sandbox-Schritt — das Maximum, das ein run_command verlangen darf — gilt nach einem Neustart des Servers",
  },
  "config.sandboxSetting.runCommand.label": { en: "Run-command timeout (seconds)", de: "Run-Command-Timeout (Sekunden)" },
  "config.sandboxSetting.runCommand.help": {
    en: "what an agent run_command gets when it asks for none; never above the step cap — applies after a server restart",
    de: "was ein run_command des Agents bekommt, wenn es nichts verlangt; nie über dem Schritt-Limit — gilt nach einem Neustart des Servers",
  },
  "config.sandboxSetting.boundNote": {
    en: "The concurrent-sandbox bound applies to the Docker backend only. A Kubernetes installation bounds sandboxes through its namespace ResourceQuota and the in-process backend is unbounded — on either, this number does nothing. Left empty it falls back to the SANDBOX_MAX_CONCURRENT environment variable, and then to the built-in default of 2; 0 means unbounded.",
    de: "Die Grenze für gleichzeitige Sandboxes gilt nur für das Docker-Backend. Eine Kubernetes-Installation begrenzt Sandboxes über die ResourceQuota ihres Namespace, und das In-Process-Backend ist unbegrenzt — bei beiden bewirkt diese Zahl nichts. Leer fällt sie auf die Umgebungsvariable SANDBOX_MAX_CONCURRENT zurück und dann auf den eingebauten Standard 2; 0 heißt unbegrenzt.",
  },
  "config.sandboxSetting.maxConcurrent.label": { en: "Max concurrent sandboxes", de: "Max. gleichzeitige Sandboxes" },
  "config.sandboxSetting.maxConcurrent.help": {
    en: "sandbox containers one Docker host runs at once — applies to the next capacity decision, no restart",
    de: "Sandbox-Container, die ein Docker-Host gleichzeitig ausführt — gilt ab der nächsten Kapazitätsentscheidung, ohne Neustart",
  },
  "config.sandboxSetting.holdNote": {
    en: "A design conversation holds the source sandboxes it opened for this long after a turn, so the next question does not pay for the clone again. Left empty it falls back to the SANDBOX_HOLD_SECONDS environment variable, and then to 180 seconds; 0 holds nothing. Per project, this can be overridden on the project itself.",
    de: "Ein Design-Gespräch hält die Quell-Sandboxes, die es geöffnet hat, so lange nach einem Turn, damit die nächste Frage nicht erneut für das Klonen zahlt. Leer fällt der Wert auf die Umgebungsvariable SANDBOX_HOLD_SECONDS zurück und dann auf 180 Sekunden; 0 hält nichts. Pro Projekt lässt sich das am Projekt selbst überschreiben.",
  },
  "config.sandboxSetting.hold.help": {
    en: "how long a design conversation keeps its source sandboxes — applies to the next capacity decision, no restart",
    de: "wie lange ein Design-Gespräch seine Quell-Sandboxes behält — gilt ab der nächsten Kapazitätsentscheidung, ohne Neustart",
  },
  "config.sandboxSetting.hardeningNote": {
    en: "On, Docker and Kubernetes sandboxes run as uid 1000 with no Linux capabilities and no privilege escalation. Check first which projects install system packages in their prerequisites (apt-get, a global npm install): those fail until the project turns on \"run sandboxes as root\" on its Sandbox tab. Off, sandboxes run as the image's user, which is root for every built-in image.",
    de: "An: Docker- und Kubernetes-Sandboxes laufen als uid 1000 ohne Linux-Capabilities und ohne Rechteausweitung. Prüf vorher, welche Projekte in ihren Voraussetzungen Systempakete installieren (apt-get, ein globales npm install): die schlagen fehl, bis das Projekt im Tab Sandbox \"Sandboxes als root ausführen\" einschaltet. Aus: Sandboxes laufen als Benutzer des Images, bei jedem eingebauten Image root.",
  },
  "config.sandboxSetting.harden": { en: "Harden sandboxes", de: "Sandboxes härten" },
  "config.sandboxSetting.afterRestart": { en: "applies after a server restart.", de: "gilt nach einem Neustart des Servers." },

  // --- settings: deployment ---
  "config.deploymentSetting.registry.label": { en: "Registry", de: "Registry" },
  "config.deploymentSetting.registry.help": {
    en: "the one-knob base registry for both images when their own field is unset",
    de: "die gemeinsame Basis-Registry für beide Images, wenn ihr eigenes Feld leer ist",
  },
  "config.deploymentSetting.version.label": { en: "Version", de: "Version" },
  "config.deploymentSetting.version.help": {
    en: "the one-knob base version applied where unset",
    de: "die gemeinsame Basis-Version, wo keine gesetzt ist",
  },

  // --- settings: limits ---
  "config.limits.scout": { en: "Scout calls", de: "Scout-Aufrufe" },
  "config.limits.maxToolCalls": { en: "Max tool calls per scout call", de: "Max. Tool-Aufrufe pro Scout-Aufruf" },
  "config.limits.maxSeconds": { en: "Max seconds per scout call", de: "Max. Sekunden pro Scout-Aufruf" },
  "config.limits.subAgents": { en: "Sub-agent caps", de: "Obergrenzen für Sub-Agents" },
  "config.limits.maxConcurrentSubAgents": { en: "Max concurrent sub-agents", de: "Max. gleichzeitige Sub-Agents" },
  "config.limits.maxSubAgentsPerRun": { en: "Max sub-agents per run", de: "Max. Sub-Agents pro Run" },
  "config.limits.maxSubAgentsPerTurn": { en: "Max sub-agents per design turn", de: "Max. Sub-Agents pro Design-Turn" },

  // --- settings: pipeline cost cap ---
  "config.costCap.usd": { en: "USD", de: "USD" },
  "config.costCap.tokens": { en: "Tokens", de: "Tokens" },
  "config.costCap.tierDefault": {
    en: "default — a run never gets less than the Default cap (${usd} / {tokens} tokens)",
    de: "Standard — ein Run bekommt nie weniger als das Standard-Limit (${usd} / {tokens} Tokens)",
  },
  "config.costCap.default": { en: "Default cap", de: "Standard-Limit" },
  "config.costCap.perTier": { en: "Per-tier caps", de: "Limits pro Stufe" },
  "config.costCap.tierCount": { en: "{count} tiers", de: "{count} Stufen" },
  "config.costCap.perPipeline": { en: "Per-pipeline overrides", de: "Überschreibungen pro Pipeline" },
  "config.costCap.setCount": { en: "{count} set", de: "{count} gesetzt" },
  "config.costCap.noOverrides": { en: "no per-pipeline overrides", de: "keine Überschreibungen pro Pipeline" },
  "config.costCap.remove": { en: "remove", de: "entfernen" },
  "config.costCap.add": { en: "Add a pipeline override", de: "Überschreibung für eine Pipeline hinzufügen" },
  "config.costCap.pickPipeline": { en: "— pick a pipeline —", de: "— Pipeline wählen —" },

  // --- settings: queue ---
  "config.queue.parallel.label": { en: "Max parallel jobs", de: "Max. parallele Jobs" },
  "config.queue.parallel.help": {
    en: "the sole backpressure knob for downstream pipeline execution",
    de: "der einzige Regler für den Gegendruck bei der nachgelagerten Pipeline-Ausführung",
  },
  "config.queue.block": { en: "Consume block (seconds)", de: "Blockieren beim Lesen (Sekunden)" },
  "config.queue.shutdown": { en: "Shutdown grace (seconds)", de: "Schonfrist beim Herunterfahren (Sekunden)" },
  "config.queue.retry.label": { en: "Redis retry interval (seconds)", de: "Wiederholintervall für Redis (Sekunden)" },
  "config.queue.retry.help": {
    en: "poll cadence when Redis is configured but unreachable",
    de: "Abfragetakt, wenn Redis eingerichtet, aber nicht erreichbar ist",
  },

  // --- settings: dialogue ---
  "config.dialogue.hotWait.label": { en: "Hot-wait window (seconds)", de: "Wartefenster im Speicher (Sekunden)" },
  "config.dialogue.hotWait.help": {
    en: "in-memory wait before an eligible run checkpoints and parks",
    de: "Wartezeit im Speicher, bevor ein geeigneter Run einen Checkpoint schreibt und parkt",
  },
  "config.dialogue.approval.label": { en: "Approval timeout (seconds)", de: "Freigabe-Timeout (Sekunden)" },
  "config.dialogue.approval.help": {
    en: "when it elapses on a parked run, the persisted default answer applies",
    de: "läuft er bei einem geparkten Run ab, gilt die gespeicherte Standardantwort",
  },
  "config.dialogue.dashboardUrl.label": { en: "Dashboard address", de: "Dashboard-Adresse" },
  "config.dialogue.dashboardUrl.help": {
    en: "a ticket comment that asks a question links to the run here",
    de: "ein Ticket-Kommentar, der eine Frage stellt, verlinkt hierher auf den Run",
  },
  "config.dialogue.adminAgent.label": { en: "Admin assistant agent", de: "Agent des Admin-Assistenten" },
  "config.dialogue.adminAgent.help": {
    en: "the configured agent the Agent Smith conversation runs on",
    de: "der eingerichtete Agent, auf dem das Agent-Smith-Gespräch läuft",
  },
  "config.dialogue.adminReadLimit.label": { en: "Admin read limit (rows)", de: "Leselimit des Admin-Assistenten (Zeilen)" },
  "config.dialogue.adminReadLimit.help": {
    en: "the most runs, events or calls one admin assistant read returns",
    de: "höchstens so viele Runs, Events oder Aufrufe liefert ein Lesevorgang des Admin-Assistenten",
  },
  "config.dialogue.adminResult.label": { en: "Admin result size (characters)", de: "Ergebnisgröße des Admin-Assistenten (Zeichen)" },
  "config.dialogue.adminResult.help": {
    en: "the most characters one admin assistant tool result carries",
    de: "höchstens so viele Zeichen trägt ein Tool-Ergebnis des Admin-Assistenten",
  },
  "config.dialogue.mockSize.label": { en: "Mock size (characters)", de: "Mock-Größe (Zeichen)" },
  "config.dialogue.mockSize.help": {
    en: "the longest HTML mock a design conversation keeps",
    de: "der längste HTML-Mock, den ein Design-Gespräch behält",
  },
  "config.dialogue.mockPerSpec": { en: "Mock versions per spec", de: "Mock-Versionen pro Spec" },
  "config.dialogue.mockPerConversation": { en: "Mock versions per conversation", de: "Mock-Versionen pro Gespräch" },
  "config.dialogue.seriesBudget.label": { en: "Series master budget (USD)", de: "Budget des Serien-Masters (USD)" },
  "config.dialogue.seriesBudget.help": {
    en: "what a series master's own judging may spend over one series; the tickets' runs have their own caps",
    de: "was die eigene Bewertung eines Serien-Masters über eine Serie kosten darf; die Runs der Tickets haben eigene Limits",
  },
  "config.dialogue.seriesBudgetAsk.label": { en: "Ask about the budget below (USD)", de: "Nach dem Budget fragen unter (USD)" },
  "config.dialogue.seriesBudgetAsk.help": {
    en: "once no more than this is left of a series master's budget, the series asks in its chat whether to raise the budget or stop; it never decides that by itself",
    de: "sobald vom Budget eines Serien-Masters nicht mehr übrig ist, fragt die Serie im Chat, ob das Budget erhöht oder die Serie gestoppt wird; sie entscheidet das nie selbst",
  },
  // 2026-11-25-433fb: the series master's thread compaction.
  "config.dialogue.seriesThreadMax.label": { en: "Series master thread limit (characters)", de: "Grenze des Serien-Master-Verlaufs (Zeichen)" },
  "config.dialogue.seriesThreadMax.help": {
    en: "past this size the series master writes notes on its decisions, open questions, acts and plan in place of its older beats, so a series that runs for days stays within its context and cost",
    de: "ab dieser Größe schreibt der Serien-Master Notizen zu Entscheidungen, offenen Fragen, Taten und Plan anstelle seiner älteren Runden, damit eine tagelange Serie in Kontext und Kosten bleibt",
  },
  "config.dialogue.seriesThreadKeep.label": { en: "Series master beats kept verbatim", de: "Wörtlich behaltene Runden des Serien-Masters" },
  "config.dialogue.seriesThreadKeep.help": {
    en: "the latest beats a compaction of the series master's thread keeps word for word",
    de: "die letzten Runden, die eine Verdichtung des Serien-Master-Verlaufs wörtlich behält",
  },
  // 2026-11-25-96aec: no cadence any more; a failed master waits this long before its next try.
  "config.dialogue.seriesStatus.label": { en: "Retry a failed series master after (minutes)", de: "Fehlgeschlagenen Serien-Master erneut versuchen nach (Minuten)" },
  "config.dialogue.seriesStatus.help": {
    en: "after a series master's turn failed, its events wait this long before it is tried again, so a failing model is not called every few seconds",
    de: "nachdem ein Durchlauf des Serien-Masters fehlgeschlagen ist, warten seine Ereignisse so lange, bevor er erneut versucht wird, damit ein fehlerhaftes Modell nicht alle paar Sekunden aufgerufen wird",
  },
  "config.dialogue.seriesSettle.label": { en: "Gather series events for (seconds)", de: "Serien-Ereignisse sammeln für (Sekunden)" },
  "config.dialogue.seriesSettle.help": {
    en: "the series master is woken once the oldest event has waited this long, so events that arrive together cost one turn; 0 wakes it at once. Your messages reach it at once",
    de: "der Serien-Master wird geweckt, sobald das älteste Ereignis so lange gewartet hat, damit gemeinsam eintreffende Ereignisse einen Durchlauf kosten; 0 weckt ihn sofort. Deine Nachrichten erreichen ihn sofort",
  },
  "config.dialogue.questionDefault.label": { en: "Question default after (minutes)", de: "Standardantwort auf Fragen nach (Minuten)" },
  "config.dialogue.questionDefault.help": {
    en: "a series question that names a recommendation takes it after this; one without waits for you",
    de: "eine Serienfrage mit Empfehlung übernimmt sie danach; eine ohne wartet auf dich",
  },

  // --- settings: skills ---
  "config.skills.source.label": { en: "Source", de: "Quelle" },
  "config.skills.source.help": { en: "how the skill catalog is resolved at boot", de: "wie der Skill-Katalog beim Start aufgelöst wird" },
  "config.skills.source.default": { en: "Default (release tag)", de: "Standard (Release-Tag)" },
  "config.skills.source.path": { en: "Path (pre-mounted directory)", de: "Pfad (vorab eingehängtes Verzeichnis)" },
  "config.skills.source.url": { en: "Url (explicit tarball)", de: "URL (explizites Tarball)" },
  "config.skills.source.embedded": { en: "Embedded (bundled in the binary)", de: "Eingebettet (im Binary enthalten)" },
  "config.skills.version.label": { en: "Version", de: "Version" },
  "config.skills.version.help": { en: "release tag for the default source", de: "Release-Tag für die Standardquelle" },
  "config.skills.path.label": { en: "Path", de: "Pfad" },
  "config.skills.path.help": {
    en: "pre-mounted catalog directory for the path source",
    de: "vorab eingehängtes Katalogverzeichnis für die Pfad-Quelle",
  },
  "config.skills.url.label": { en: "Url", de: "URL" },
  "config.skills.url.help": { en: "explicit tarball url for the url source", de: "explizite Tarball-URL für die URL-Quelle" },
  "config.skills.sha.label": { en: "SHA256", de: "SHA256" },
  "config.skills.sha.help": {
    en: "optional tarball hash for a downloading source",
    de: "optionaler Tarball-Hash für eine herunterladende Quelle",
  },
  "config.skills.cacheDir.label": { en: "Cache directory", de: "Cache-Verzeichnis" },
  "config.skills.cacheDir.help": {
    en: "where downloaded catalogs are extracted (empty = default)",
    de: "wohin heruntergeladene Kataloge entpackt werden (leer = Standard)",
  },

  // --- settings: data flow, trace, registries ---
  "config.dataFlow.enforce": { en: "Enforce data-flow gating", de: "Datenfluss-Prüfung durchsetzen" },
  "config.trace.enabled": {
    en: "Record each run's conversation (AGENTSMITH_TRACE, when set, wins)",
    de: "Die Unterhaltung jedes Runs aufzeichnen (AGENTSMITH_TRACE hat Vorrang, wenn gesetzt)",
  },
  "config.registries.label": { en: "private package feeds", de: "private Paket-Feeds" },
  "config.registries.help": {
    en: "this project's runs sign in to these feeds when a repository's nuget.config or .npmrc names them",
    de: "die Runs dieses Projekts melden sich an diesen Feeds an, wenn die nuget.config oder .npmrc eines Repositorys sie nennt",
  },
  "config.registries.none": { en: "no private feeds configured", de: "keine privaten Feeds eingerichtet" },
  "config.registries.feed": { en: "Feed {n}", de: "Feed {n}" },
  "config.registries.host": { en: "Host", de: "Host" },
  "config.registries.username": { en: "Username", de: "Benutzername" },
  "config.registries.usernamePlaceholder": { en: "any", de: "beliebig" },
  "config.registries.token.label": { en: "Token", de: "Token" },
  "config.registries.token.help": { en: "the secret reference for the feed token", de: "der Secret-Verweis für das Feed-Token" },
  "config.registries.remove": { en: "Remove feed", de: "Feed entfernen" },
  "config.registries.add": { en: "Add a feed", de: "Feed hinzufügen" },

  // --- settings: branch cleanup ---
  "config.branchCleanup.enabled": { en: "Run branch cleanup", de: "Branch-Bereinigung ausführen" },
  "config.branchCleanup.interval.label": { en: "Interval (seconds)", de: "Intervall (Sekunden)" },
  "config.branchCleanup.interval.help": {
    en: "seconds between two branch-cleanup runs of a project; each run spends host API calls",
    de: "Sekunden zwischen zwei Bereinigungsläufen eines Projekts; jeder Lauf kostet API-Aufrufe beim Host",
  },
} satisfies Record<string, MessageEntry>;
