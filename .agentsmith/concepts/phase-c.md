# Phase C — Konkretisierung

**Zweck:** Skill-Format, Plan/Diff-Schemas, Pipeline-Implementations-Anforderungen.
**Voraussetzung:** Phase A, Phase B.

Drei Sektionen:
1. Skill-Format
2. Plan- und Diff-Schemas
3. Pipeline-Implementation

---

## 1. Skill-Format

Ein Skill ist eine Markdown-Datei mit YAML-Frontmatter und Markdown-Body. **Eine Datei, ein Body, ein Job.**

### Frontmatter-Felder

| Feld | Pflicht | Typ | Bedeutung |
|---|---|---|---|
| `name` | ja | string | Lowercase mit Bindestrich, projekt-eindeutig (`api-vuln-investigator`) |
| `version` | ja | string | semver, z. B. `1.0.0` |
| `role` | ja | enum | `producer`, `investigator`, `judge`, `filter` |
| `description` | ja | string | Ein Satz, max. 200 Zeichen, für Triage-Anzeige |
| `activates_when` | ja | bool-expr | Boolescher Ausdruck über Konzepte (siehe Phase B) |
| `output_schema` | ja | enum | `observation`, `plan`, `diff`, `bootstrap` |
| `category` | bei `investigator_mode: verify_hint` Pflicht, sonst optional | string | Identifiziert den Zuständigkeitsbereich. Für VerifyHint zentral, weil Cluster-Wahl per Mehrheits-Category läuft. Empfohlen aus geschlossenem enum (`auth`, `injection`, `secrets`, `iam`, `crypto`, …) zur Vermeidung von Drift (`auth` vs. `authentication`) |
| `investigator_mode` | nur bei `role: investigator` | enum | `verify_hint`, `survey` oder `verify_diff` |
| `survey_scope` | nur bei `survey` | string-list | Repo-Bereiche als Glob-Pattern (`src/**/*.cs`) |
| `scope_hint` | nein | string | Hint an den Skill, auf welchen Repo-Bereich er sich freiwillig einschränkt — wird in Skill-Prompt injiziert, ist kein Erzwingungs-Mechanismus. Keine Pfad-Validierung beim Skill-Load (Hint ist freier Text), und ein Skill mit unsinnigem Hint funktioniert weiter — der Hint hat dann nur keinen Effekt |
| `block_condition` | nur bei `role: judge` | string | Klartext-Bedingung, wann der Beurteiler `blocking: true` setzt |
| `loop` | nein | bool | Tool-Loop aktivieren. Default `true` für Produzenten und Untersucher, `false` für Beurteiler und Filter. Beurteiler/Filter können `loop: true` setzen, wenn ihr Job es erfordert — Ausnahme, nicht Default |

### Validierung beim Skill-Load

| Regel | Konsequenz |
|---|---|
| Pflichtfelder fehlen | Skill abgelehnt, Build-Time-Fehler |
| `name` nicht eindeutig | Skill abgelehnt |
| `activates_when` referenziert nicht-deklariertes Konzept | Skill abgelehnt |
| `output_schema` passt nicht zur `role` (z. B. judge mit `plan`) | Skill abgelehnt |
| `output_schema: bootstrap` mit `role` nicht `producer` | Skill abgelehnt |
| `investigator_mode` fehlt bei `role: investigator` | Skill abgelehnt |
| `survey_scope` fehlt bei `investigator_mode: survey` | Skill abgelehnt |
| `category` fehlt bei `investigator_mode: verify_hint` | Skill abgelehnt |
| `block_condition` fehlt bei `role: judge` | Skill abgelehnt |
| Body fehlt oder leer | Skill abgelehnt |

### Body-Format

Markdown-Prosa, **eine** Sektion. Kein `## as_<role>`-Trennung mehr — die Rolle ist im Frontmatter.

Empfohlene Body-Struktur (nicht erzwungen, aber vom Format-Linter gechecked):

```
## Aufgabe
[Was tut dieser Skill, in 2-3 Sätzen]

## Disziplin
[Was er nicht tun soll, klare Verbote]

## Vorgehen
[Wie er an die Aufgabe rangeht]

## Output-Felder
[Wenn das Output-Schema mehrere Verwendungen kennt: was bedeuten die Felder im Kontext dieses Skills]
```

### Output-Format-Disziplin

Unabhängig vom Schema gilt für *jeden* Skill-Output:
- JSON, einzeilig, kein Preamble, kein Markdown-Fence.
- Nur Felder aus dem deklarierten Schema, keine zusätzlichen.
- Bei mehreren Items: JSON-Array. Bei einem Item: trotzdem JSON-Array mit einem Eintrag (Konsistenz).

### Skill-Discovery

Skills liegen in einem Verzeichnis pro Repository (`agent-smith-skills/skills/<skill-name>/SKILL.md`). Pro Skill ein eigenes Verzeichnis. Beim Server-Boot werden alle SKILL.md-Dateien geparst, validiert und in den Skill-Index aufgenommen. Der Pfad zum Skill-Verzeichnis ist konfigurierbar in `agentsmith.yml` (Feld `skills_path`).

Validierung beim Boot: jeder Skill muss alle Pflichtfelder haben, der Aktivierungs-Ausdruck muss parsen und nur deklarierte Konzepte referenzieren, das Output-Schema muss zur Rolle passen. Ungültige Skills werden mit klarem Fehler abgelehnt — der Server bootet weiter mit den verbleibenden gültigen Skills.

---

## 2. Plan- und Diff-Schemas

Beide JSON, einzeilig. Schemas sind Pflicht-validiert vor Pipeline-Übergabe.

### Plan-Schema

Genutzt von: Produzent in Plan-Phase.

```json
{
  "summary": "string, ein Satz, max. 200 Zeichen",
  "scope": {
    "files": ["string"],
    "modules": ["string, frei — Namespace, Package, Layer-Pfad oder Modul-Name aus context.yaml"]
  },
  "steps": [
    {
      "id": 1,
      "action": "string, was wird getan",
      "file": "string, Pfad",
      "reason": "string, warum, max. 300 Zeichen"
    }
  ],
  "open_questions": [
    {
      "id": "string, q1/q2/...",
      "question": "string",
      "options": ["string"]
    }
  ],
  "test_impact": "string oder null",
  "consumer_impact": "string oder null",
  "status": "complete | needs_user_input"
}
```

**Pflichtfelder:** `summary`, `scope`, `steps`, `status`. Andere optional, müssen aber wenn vorhanden valide sein.

**Verhalten:**
- `open_questions` haben keinen Default. Wenn der Plan-Skill eine Frage offen lassen muss, läuft sie zwingend über die Eskalations-Route — der Mensch entscheidet, der Skill darf nicht selbst raten.
- `status: needs_user_input` und `open_questions` ist leer → invalid, `failed_validation`.
- `status: complete` und `open_questions` ist nicht leer → invalid, `failed_validation` mit Hinweis: *"complete erfordert leere open_questions; sonst needs_user_input setzen"*.
- `scope.files` enthält Datei außerhalb des Aktivierungs-Bereichs → `failed_validation`.

### Diff-Schema

Genutzt von: Produzent in Implementation-Phase.

```json
{
  "changes": [
    {
      "file": "string, Pfad",
      "operation": "modify | add | delete",
      "summary": "string, max. 200 Zeichen",
      "patch": "string, unified-diff-Format"
    }
  ],
  "tests_added": [
    {
      "file": "string",
      "summary": "string"
    }
  ],
  "tests_modified": [
    {
      "file": "string",
      "summary": "string"
    }
  ],
  "build_status": "ok | failed | not_run",
  "test_status": "ok | failed | not_run"
}
```

**Pflichtfelder:** `changes`, `build_status`, `test_status`.

**Verhalten:**
- `changes` enthält Datei nicht aus dem freigegebenen Plan → `failed_validation`.
- `build_status: failed` oder `test_status: failed` → Implementation gilt als unvollständig, Status `incomplete`. Pipeline entscheidet (siehe Phase B).
- `not_run` ist erlaubt bei `mad`-artigen Pipelines. In fix-bug und feature-implementation: `failed_validation` mit Hinweis *"build muss laufen, run dotnet build vor finaler Antwort"*.

### Observation-Schema

Existiert. Wird nicht angefasst. Siehe `observation-schema.md`. Wird hier nur referenziert.

### Bootstrap-Output (init-project)

Sonderfall, kein JSON-Schema im engeren Sinn. Der init-project-Skill schreibt zwei Dateien direkt ins Repo: `context.yaml` (Schema siehe bestehende Datei im Projekt) und `coding-principles.md` (frei strukturierter Markdown-Text mit Naming, Test-Style, Sprache).

Statt eines JSON-Outputs liefert der Bootstrap-Skill als Skill-Call-Ergebnis eine Bestätigungs-Nachricht: welche Dateien geschrieben wurden, ob `open_questions` an den Menschen gehen müssen (analog zum Plan-Schema), Status `complete` oder `needs_user_input`. Validierung beschränkt sich auf: existieren beide Dateien danach im Repo? Sind sie syntaktisch parsbar (YAML, Markdown)?

**Beispiel-Frontmatter eines Bootstrap-Skills:**

```yaml
name: project-bootstrap
version: 1.0.0
role: producer
description: Erzeugt context.yaml und coding-principles.md für ein neues Repository.
activates_when: pipeline_name = "init-project"
output_schema: bootstrap
loop: true
```

Bootstrap-Skills laufen nur in der init-project-Pipeline. Üblicherweise gibt es genau einen Bootstrap-Skill pro Sprach-/Framework-Familie (.NET, Node, Python, …) — `activates_when` erweitert sich dann um Sprach-Konzepte aus dem Run-State (z. B. `pipeline_name = "init-project" AND project_language = "csharp"`).

---

## 3. Pipeline-Implementation

Pipelines werden **im Code** als Command-Sequenzen implementiert, nicht als deklarative YAML-Files. Eine YAML-Pipeline-Spec danebenzulegen würde Doppel-Quelle der Wahrheit und Drift erzeugen — bei Diskrepanz gewinnt der Code, die YAML lügt.

Phase A definiert die fünf Arbeits-Pipelines (fix-bug, feature-implementation, mad-discussion, api-security-scan, security-scan) plus die Bootstrap-Pipeline init-project mit ihren Phasen, Rollen pro Phase, Skill-Anzahl und Output-Schemas. Phase C ergänzt: was eine Pipeline-Implementation im Code nachweislich erfüllen muss.

### Anforderungen an jede Pipeline-Implementation

| Anforderung | Bedeutung |
|---|---|
| Phasen entsprechen Phase A | Reihenfolge, Anzahl, Namen wie in der Phase-A-Tabelle |
| Rolle pro Phase entspricht Phase A | Producer, Investigator, Judge, Filter, oder LLM-frei |
| Skill-Anzahl entspricht Phase A | Feste Zahl oder `n`, durchsetzt durch die Skill-Auswahl-Logik |
| Datenflüsse opt-in | Jede Phase sieht nur Outputs anderer Phasen, die explizit weitergegeben werden (siehe Phase B) |
| Block-Handling wie Phase B | Erster Block → Re-Plan, zweiter Block → Eskalation ans Ticket |
| Output-Schemas validiert | Vor Phasen-Übergabe wird der Output gegen das deklarierte Schema validiert. Ungültiges JSON → `failed_parse`, gültiges JSON mit Schema-/Regelverletzung → `failed_validation`. Sonderfall `output_schema: bootstrap`: keine JSON-Validierung — stattdessen Datei-Existenz (`context.yaml`, `coding-principles.md`) und syntaktische Parsbarkeit (YAML, Markdown) prüfen. Verletzung → `failed_validation`. |
| Konzepte werden publiziert | Pre-Skill-Handler schreiben deklarierte Konzepte in den Run-State, bevor Triage läuft. Post-Skill-Handler (pro Phase) leiten Konzepte aus Skill-Output ab und schreiben sie für nachfolgende Phasen. |
| Pre-Filter vor Investigate (nur api-security-scan) | api-security-scan muss einen regel-basierten Pre-Filter zwischen Discover und Investigate ausführen. Form siehe Phase B. security-scan (Survey) hat keine Vor-Findings und damit keinen Pre-Filter. |
| Loop-Limits enforced | Limits aus `agentsmith.yml` werden pro Skill-Call durchgesetzt |

### Skill-Auswahl pro Phase

Die Skill-Auswahl ist Code-Logik, kein YAML. Sie folgt diesen Schritten:

1. Triage liest alle geladenen Skills.
2. Filtert auf `role` der Phase.
3. Filtert auf Skills, deren `activates_when` über aktuellem Run-State `true` ist. *Das* ist der Mechanismus für *"ggf. UX-Beurteiler"* — der UX-Beurteiler hat `activates_when: ui_changes_present`, läuft nur wenn das Konzept gesetzt ist.
4. Bei mehr passenden Skills als die Phase erlaubt: Spezifitäts-Auswahl (mehr Konzept-Referenzen = spezifischer; Tie-Break lex nach Skill-Name).
5. Bei weniger passenden Skills als die Phase mindestens braucht: Pipeline-Fehler, Eskalation an Mensch.

### Pipeline-Dokumentation für Menschen

Jede Pipeline-Implementation hat eine begleitende Markdown-Datei (`docs/pipelines/<pipeline-name>.md`). Diese Datei ist **Lese-Doku, keine technische Spec, keine Quelle der Wahrheit**:

- Kurz-Beschreibung der Pipeline (wofür, typischer Trigger).
- Verweis auf die Phase-A-Tabelle für Phasen + Rollen.
- Diagramm der Datenflüsse (ASCII oder Mermaid).
- Block- und Eskalations-Verhalten in Worten.
- Hinweise zu typischen Skill-Konfigurationen.

**Unterschied zu YAML-Pipeline-Specs:** YAML würde von einem Validator/Loader gelesen und gegen den Code abgeglichen — bei Drift gewinnt der Code, die YAML wäre eine schlafende Lüge im Loader. Markdown wird *gar nicht maschinenlesbar verarbeitet*. Sie ist Lese-Material für Menschen, nichts mehr. Bei Drift wird sie nachgezogen wie jede andere Doku-Datei — niemand bezieht sich auf sie als Quelle.

Das ist nicht "auch Doppel-Quelle, nur in Markdown". Eine Doppel-Quelle würde bedeuten: zwei Stellen, die beide *verarbeitet* werden und sich widersprechen können. Hier wird nur eine Stelle verarbeitet (Code), die andere ist Lesehilfe.

### Was nicht als YAML existiert

- Pipeline-Definitionen
- Phasen-Sequenzen
- Datenfluss-Deklarationen *zwischen Phasen einer Pipeline* (wohlgemerkt: Datenflüsse sind im Code als gerichtete Übergaben sichtbar — kein YAML-Schema dafür)
- Block-Handling-Konfiguration

### Was als YAML existiert

- `agentsmith.yml` — globale Limits und Pfade
- `concepts.yaml` — Konzept-Vokabular
- `pre-filters.yaml` — Pre-Filter-Regeln
- `context.yaml` — Architektur-Topologie pro Repo
- Skill-Frontmatter — pro Skill, Triage-relevant

### Plan- und Diff-Persistenz

Plan und Diff werden während eines Pipeline-Runs in beide Formen geschrieben:

- **Datei** unter `.agentsmith/runs/<run-id>/plan.json` bzw. `diff.json` — für Replay (Run nochmal laufen lassen mit demselben Input), CI-Integration (Plan vor Implementation extrahieren), Debug (Klartext lesen).
- **Pipeline-Storage** (Redis/In-Memory) — für laufenden Phasen-Übergang.

Bei Pipeline-Ende: Datei bleibt, Pipeline-Storage wird verworfen. Bei Replay liest die Pipeline aus der Datei.

---

## Erfolgskriterium

Ein Mensch liest Phase C und kann ohne Rückblättern:
1. Eine SKILL.md für `bug-fixer-architect-judge` schreiben (Frontmatter + Body-Skelett).
2. Ein Plan-JSON für ein triviales Ticket (Status-Code-Korrektur) ausfüllen.
3. Eine Pipeline-Implementation skizzieren — welche Phasen sie hat, welche Rolle pro Phase, welche Datenflüsse, was bei Block passiert — ohne dafür YAML zu erfinden.

Wenn nicht — zurück, nicht weiter.

---

## Bewusst offen für spätere Phasen

- Implementation-Reihenfolge: welche Phase zuerst gebaut wird, in welchen Schritten — Phase D.
- Migrations-Pfad: heutige Multi-Body-Skills, `roles_supported`, Code-Map-Generation — Phase D oder G.
- Test-Strategie: wie das Modell verifiziert wird ohne den ganzen Stack zu bauen — Phase D.
- Skill-Lifecycle: Versionierung, Update, Deprecation — Phase E oder F.
- Ticket-Writeback-Format: wie Eskalations-Outputs ans Ticket gelangen — Phase E.
- autonomous-Pipeline: Agent Smith schreibt selbst Tickets — nach Phase G.