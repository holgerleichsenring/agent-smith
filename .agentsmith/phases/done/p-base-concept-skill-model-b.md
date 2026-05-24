# Phase B — Infrastruktur

**Zweck:** Wie das Modell aus Phase A technisch lebt — Mechanismen und Schnittstellen, keine Klassennamen.
**Voraussetzung:** Phase A.

---

## Übersicht der Mechanismen

| Mechanismus | Wofür |
|---|---|
| Run-State + Konzepte | Aktivierung deterministisch machen |
| Aktivierungs-Ausdruck | Pro Skill, boolesch über Konzepte |
| Tool-Loop | Skill-Call mit mehreren LLM- und Tool-Calls |
| Loop-Limits | Kosten begrenzen |
| Skill-Sequencing | Wie n Skills innerhalb einer Phase laufen |
| Pre-Filter | LLM-freie Reduktion vor Investigate |
| Re-Plan-Mechanik | Plan zurück, Notizen aggregiert, erneutes Review |
| Plan/Diff-Persistenz | Plan und Diff durch die Pipeline tragen |

---

## Run-State und Konzepte

**Run-State** ist der Pipeline-Zustand zur Laufzeit — Fakten, die während des Runs feststehen. Konzepte sind benannte boolesche oder numerische Fakten im Run-State.

**Konzepte sind die triage-relevante Untermenge des Run-States.** Andere Run-State-Daten (Datei-Pfade, Tool-Outputs, Phasen-Artefakte wie Plan und Diff, Findings-Listen, Ticket-Text) leben ebenfalls im Run-State, sind aber nicht Teil des Konzept-Vokabulars und werden nicht von Aktivierungs-Ausdrücken referenziert. Sie fließen als Input zwischen Phasen über die Sichtbarkeitsregeln (siehe unten).

**Wer schreibt Konzepte:**
- Pre-Skill-Handler (Source-Checkout, Swagger-Loader, Scanner-Runner, Bootstrap-Checker) schreiben harte Fakten: `source_available: true`, `swagger_spec_present: true`, `nuclei_findings_count: 102`, `context_yaml_present: true`, `coding_principles_present: true`.
- Post-Skill-Handler (einer pro Phase pro Pipeline-Implementation) schreiben abgeleitete Konzepte aus dem Skill-Output: `plan_complete: true` nach erfolgreicher Plan-Phase, `review_passed: true` nach Review ohne Block, `verify_passed: true` nach Verify ohne Fail. An diesen Handlern ist kein LLM beteiligt — sie sind Code im Pipeline-Setup, der Skill-Outputs liest und Konzepte ableitet.
- Skills schreiben *keine* Konzepte. Skills lesen Konzepte über Aktivierungs-Ausdrücke und ihren Input.

**Form eines Konzepts:**
- Name: lowercase mit Unterstrich, Pflicht-Präfix nach Kategorie. Präfixe sind extensibel, das Vokabular deklariert die erlaubten. Beispiele: `source_*`, `findings_*`, `plan_*`, `review_*`, `swagger_*`, `nuclei_*`, `api_*`.
- Wert: bool, int oder enum aus geschlossenem Vokabular. Keine freien Strings.
- Quelle: jeder Konzept-Eintrag im Run-State trägt den Namen des Schreibers (welcher Handler hat's gesetzt).
- Default bei fehlendem Konzept: typ-abhängig — `false` für bool, `0` für int, der erste Wert im enum für enum-Konzepte. Das Vokabular deklariert den Typ explizit.

**Konzept-Vokabular:**
- Globale YAML-Datei `concepts.yaml` listet alle erlaubten Konzepte mit Typ, Wertebereich, Beschreibung, Schreiber-Liste.
- Schreiber, die ein nicht-deklariertes Konzept setzen, scheitern beim Start (Build-Time-Validation der Handler).
- Aktivierungs-Ausdrücke, die ein nicht-deklariertes Konzept lesen, scheitern beim Skill-Load (Skill wird abgewiesen).
- Standard-Konzept `pipeline_name` (enum) ist Pflicht im Vokabular und wird vom Pipeline-Setup gesetzt. Skills können damit auf die laufende Pipeline reagieren (z. B. `chain-analyst` mit `pipeline_name = "api-security-scan" OR pipeline_name = "security-scan"`). Pipeline-spezifische abgeleitete Konzepte wie `plan_complete` liegen im selben globalen Namespace; `pipeline_name` differenziert bei Bedarf.

---

## Aktivierungs-Ausdruck

**Form:** boolescher Ausdruck mit Operatoren `AND`, `OR`, `NOT`, Klammern, Vergleichen (`=`, `>`, `>=`, `<`, `<=`).

**Beispiele:**
```
source_available AND swagger_spec_present
findings_count > 0 AND NOT api_internal_only
plan_phase = "review" AND review_target = "architecture"
```

**Wo das lebt:** im Skill-Frontmatter, Feld `activates_when`. Eine einzige Zeile (oder Block).

**Evaluierung:** deterministisch zur Triage-Zeit. Triage liest Run-State, evaluiert pro Skill den Ausdruck, schaltet Skill ein oder aus. Kein LLM beteiligt. Wenn ein Konzept im Run-State fehlt, gilt der typ-abhängige Default (`false` für bool, `0` für int, erster enum-Wert).

**Was raus ist:** `activation.positive`/`negative`-Listen, `role_assignment.*`-Whitelists. Ein Ausdruck pro Skill, fertig.

---

## Tool-Loop

**Was ein Skill-Call ist:** keine einzelne LLM-Anfrage mehr. Ein Skill-Call ist eine **Loop-Sitzung** mit:

1. Initial-Prompt (Skill-Body + Input + Konzept-Snapshot)
2. LLM-Antwort, die entweder Tool-Aufrufe (`read`, `grep`, `find`, evtl. `bash`) oder eine finale Antwort enthält
3. Bei Tool-Aufrufen: Tools werden ausgeführt, Ergebnisse zurück an LLM, neue LLM-Antwort
4. Loop bis finale Antwort *oder* Limit erreicht

**Verfügbare Tools (Loop-Vokabular):**

| Tool | Wer darf | Zweck |
|---|---|---|
| `read` | alle mit Loop | Datei lesen (Pfad muss in Aktivierungs-Bereich liegen) |
| `grep` | alle mit Loop | Im Aktivierungs-Bereich suchen |
| `find` | alle mit Loop | Dateien finden (Aktivierungs-Bereich) |
| `bash` (Build/Test) | Implementation-Phase und Verify-Phase | Build- und Test-Kommandos. Implementation um den Plan umzusetzen, Verify um den Diff unabhängig zu prüfen. |
| `write` | nur Implementation-Phase und Bootstrap-Phase | Datei schreiben — Implementation: nur Dateien aus freigegebenem Plan. Bootstrap: nur `context.yaml` und `coding-principles.md`. |

**Aktivierungs-Bereich:** Skills mit Tool-Loop dürfen alles lesen, was im Repo Git-tracked ist (außerhalb von `.gitignore`). Schreibzugriff ist eingeschränkt:
- **Implementation-Phase:** nur Dateien aus dem freigegebenen Plan.
- **Bootstrap-Phase (init-project):** nur die Voraussetzungs-Dateien `context.yaml` und `coding-principles.md` im Repo-Root. Keine andere Datei.
- **Alle anderen Phasen:** kein Schreibzugriff.

Begrenzung übermäßiger Suche erfolgt nicht durch Pfad-Whitelist, sondern durch die Loop-Limits — 10–30 Tool-Calls pro Skill-Call sind kein Budget für willkürliches Repo-Streifen. Untersucher (Survey) haben zusätzlich konfigurierte Repo-Bereiche aus der Pipeline-Implementation, falls die Survey-Aufgabe das erfordert.

Skills können ihren eigenen Lese-Bereich freiwillig im Frontmatter einschränken (Feld `scope_hint`). Der Hint wird in den Skill-Prompt injiziert (z. B. *"Konzentriere deine Suche auf `Application/**`"*) — kein Erzwingungs-Mechanismus, sondern Hinweis an den LLM. Repo-Topologie (Module, Layer-Pfade) wird nicht statisch deklariert — sie ist Sache des Tool-Loops, der den Code im Run liest.

Beurteiler und Filter haben in der Regel keinen Loop. Sie können aber einen aktivieren (Frontmatter-Feld `loop: true`), wenn der Skill-Job es erfordert. In dem Fall gelten dieselben Limits wie für Untersucher (10 Tool-Calls). Tool-Loop für Beurteiler ist Ausnahme, nicht Default — der Vier-Augen-Sinn der Review ist gerade, dass jemand prüft *ohne* in den Code zu schauen.

---

## Loop-Limits

**Globale Defaults**, konfigurierbar in `agentsmith.yml` unter `limits`. Nicht pro Projekt.

| Limit | Default | Geltungsbereich |
|---|---|---|
| `max_tool_calls_per_skill` | 30 | Plan- und Implementation-Skills |
| `max_tool_calls_per_investigator` | 10 | Untersucher (VerifyHint und Survey) |
| `max_tool_calls_per_verifier` | 20 | Untersucher in Verify-Phase (VerifyDiff). Höher, weil Diff plus Test-Output plus referenzierter Code zusammen oft mehr als 10 Reads brauchen. |
| `max_llm_calls_per_skill` | 15 | Alle Skills mit Loop |
| `max_input_tokens_per_skill_call` | 200_000 | Über die ganze Loop-Sitzung kumuliert |
| `max_output_tokens_per_skill_call` | 16_000 | Über die ganze Loop-Sitzung kumuliert |
| `max_seconds_per_skill_call` | 300 | Wall-Clock-Limit als Last-Resort |
| `max_concurrent_skill_calls` | 10 | Pro Pipeline-Run, schützt LLM-Provider-Rate-Limits. Bei mehreren parallelen Pipeline-Runs hat jeder seinen eigenen Pool — Provider-Rate-Limits müssen extern geregelt werden (Account-Quota). |

**Skill-Call-Status:** fünf Werte, jeder Skill-Call endet mit genau einem.

| Status | Bedeutung |
|---|---|
| `ok` | Output da, valide gegen erwartetes Schema |
| `incomplete` | Output da, aber Limit gerissen (siehe unten) — möglicherweise unvollständig |
| `failed_parse` | LLM hat geantwortet, aber Output ist kein gültiges JSON |
| `failed_validation` | Output ist gültiges JSON, aber verletzt semantische Regeln (Pfad außerhalb Bereich, Datei nicht aus Plan, `build_status: not_run` wo Build laufen müsste etc.) |
| `failed_runtime` | Skill-Call hat Exception geworfen (Timeout, Tool-Crash, Network) |

**Verhalten bei Limit (`incomplete`):**
- Erreicht der Loop ein Tool-Call- oder LLM-Call-Limit: nächster LLM-Aufruf erhält Hinweis *"Limit fast erreicht, gib jetzt finale Antwort"*. Liefert der nicht final ab, wird der bisherige Output mit Status `incomplete` zurückgegeben.
- Erreicht der Loop ein Token- oder Zeit-Limit: harter Abbruch, Status `incomplete`.

**Verhalten bei `failed_parse`:**
- Automatischer Re-Try mit *"letzter Output war kein gültiges JSON"* als zusätzlicher Hinweis. Re-Try-Limit: einmal.
- Bei zweitem Fehlschlag: direkte Eskalation (in Solo-Phase ans Ticket, in paralleler Phase als Skill-Ausfall im Run-Aggregat). Kein Backoff-Retry — `failed_parse` ist deterministisch (LLM kann an dieser Stelle kein gültiges JSON), nicht transient.

**Verhalten bei `failed_validation`:**
- Automatischer Re-Try mit konkretem Validierungs-Fehler als Hinweis (z. B. *"Datei `X.cs` ist nicht im freigegebenen Plan"*, *"build_status muss `ok` oder `failed` sein, nicht `not_run`"*). Re-Try-Limit: einmal.
- Bei zweitem Fehlschlag: direkte Eskalation (analog `failed_parse`). Kein Backoff.

**Verhalten bei `failed_runtime`:**
- Skill-Call wird als *"no output"* behandelt.
- In parallelen Phasen (Review, Investigate, Discuss): Phase läuft mit den Outputs der anderen Skills weiter. Die Pipeline-Engine notiert den Ausfall im Run-Aggregat.
- In Solo-Phasen (Plan, Implementation, Filter, Synthesize): ein Retry mit Backoff (Default 2 Sekunden, dann 8 Sekunden) für transiente Fehler (Network, Rate-Limit). Nach drittem Fehlschlag: Pipeline endet, Eskalation ans Ticket.

**Pipeline-Verhalten bei `incomplete`:**
- Output landet in der Aggregation, mit Warnung an die nächste Phase.
- Beurteiler in Review-Phasen werden auf `incomplete`-Inputs explizit hingewiesen — sie können das in ihre Block-Entscheidung einbeziehen.

**Cost-Tracking:**
- Pro Skill-Call werden Tool-Call-Anzahl, LLM-Call-Anzahl, Input-Tokens, Output-Tokens, Wall-Clock-Zeit gemessen.
- Aggregat pro Pipeline-Run: Summe über alle Skill-Calls, gruppiert nach Phase und Skill.
- Im Pipeline-Ergebnis ausgewiesen, im Ticket-Writeback enthalten.

---

## Skill-Sequencing innerhalb einer Phase

Eine Phase kann mehrere Skills haben. Wie sie laufen, hängt von der Phase ab.

| Phase-Typ | Sequencing | Geteilter Kontext |
|---|---|---|
| Plan (1 Skill) | n/a | n/a |
| Review (n Beurteiler) | parallel | jeder sieht denselben Plan, niemand sieht andere Beurteiler-Ausgaben |
| Implementation (1 Skill) | n/a | n/a |
| Verify (n Untersucher) | parallel | jeder sieht denselben Diff + Build/Test-Output, niemand sieht andere Verify-Ausgaben |
| Discuss (n mad-Produzenten) | parallel | keiner sieht andere |
| Investigate VerifyHint (n Untersucher × m Findings) | parallel pro Cluster | keiner sieht andere |
| Investigate Survey (n Untersucher) | parallel | keiner sieht andere |
| Filter | sequenziell, ein Skill | Input ist die akkumulierte Observation-Liste |
| Synthesize | sequenziell, ein Skill | Input ist die gefilterte Observation-Liste |
| Bootstrap (init-project, 1 Skill) | n/a | n/a |

**Geteilter Kontext immer aus:** kein Skill sieht den Output eines anderen Skills derselben Phase. Begründung: Determinismus (Reihenfolge spielt keine Rolle), Parallelisierbarkeit, kein Bias durch früheren Skill.

**Was wenn Beurteiler-Notizen aufeinander Bezug nehmen müssten:** dürfen sie nicht. Wenn der Architekt einen Punkt macht, den der Tester aufgreifen würde — beide schreiben unabhängig, der Re-Planner sieht beide Notizen und integriert sie. Synthese ist Re-Planner-Job, nicht Beurteiler-Job.

---

## Pre-Filter ohne LLM

**Wann:** vor jeder Investigate-Phase in api-security-scan und security-scan.
**Wie:** regel-basiert über Konzept-Vokabular und Observation-Felder.

**Form einer Regel** (in `pre-filters.yaml`, global):
```
- match:
    severity: ["info", "low"]
    category: ["formatting", "documentation"]
  action: drop
- match:
    confidence: { lt: 30 }
  action: drop
- match:
    file: { glob: "**/test/**" }
    category: ["secrets"]
  action: drop
```

**Was Pre-Filter nicht ist:** keine LLM-Bewertung, keine Confidence-Anpassung, keine Umformulierung. Drop-or-keep, deterministisch.

**Reihenfolge:** Pre-Filter läuft *nach* Discover, *vor* Investigate. Die nach Pre-Filter übrig gebliebenen Observations gehen in Investigate-Cluster (VerifyHint) oder werden ignoriert (Survey nutzt Pre-Filter nicht — Survey hat keine Vor-Findings).

---

## VerifyHint-Clustering

**Cluster-Schlüssel:** `(file, api_path, schema_name)` — die strukturellen Lokations-Felder aus dem Observation-Schema. Zwei Findings im selben Cluster, wenn alle drei übereinstimmen.

**Cluster-Limit:** ein Cluster mit mehr als 5 Findings wird in mehrere Cluster gleicher Größe geteilt — verhindert Mega-Cluster, in denen ein Untersucher 30 Findings im einen Loop bearbeiten müsste. Splitting deterministisch per FIFO (Findings in Eingangs-Reihenfolge in Sub-Cluster der Größe 5 verteilt).

**Untersucher-Wahl pro Cluster:** der Skill, dessen Aktivierungsbedingung passt und dessen `category` mit der Cluster-Mehrheits-Category übereinstimmt. Bei keiner Mehrheit (z. B. `[auth, injection, secrets]`) wird die alphabetisch erste Category gewählt — deterministisch.

**Spezifität bei mehreren passenden Skills:**
- Erstrangig: Skill mit mehr Konzept-Referenzen im Aktivierungs-Ausdruck (mehr Conjuncts = spezifischer).
- Tie-Break: lexikografisch nach Skill-Name.

**Eskalation:** wenn die erste Untersucher-Antwort eines Clusters Confidence < 60 hat, läuft eine zweite Perspektive (anderer passender Skill). Beide Antworten gehen in die Pipeline-Aggregation, der Filter entscheidet welche behält.

---

## Re-Plan-Mechanik

**Trigger:** mindestens ein Beurteiler in der Review-Phase liefert eine Observation mit `blocking: true`.

**Aggregation:**
- *Alle* Beurteiler-Observations (blocking und non-blocking) werden gesammelt.
- Sie werden dem Plan-Skill als zusätzlicher Input mitgegeben — Format: Liste der Observations sortiert nach `severity`, `confidence`.
- Der Plan-Skill bekommt zusätzlich den vorigen Plan als Input.

**Re-Plan-Lauf:**
- Plan-Skill läuft mit demselben Aktivierungs-Bereich wie ursprünglich plus den neuen Inputs.
- Tool-Loop-Limits sind dieselben — kein Bonus für Re-Plan. Wenn der Re-Plan-Lauf den Status `incomplete` zurückgibt (Limit gerissen), wird er wie ein Block behandelt: Eskalation ans Ticket. Mehr LLM-Iterationen heilen ein Ticket nicht, das schon im ersten Re-Plan ans Limit kommt.
- Output: neuer Plan im selben Schema.

**Zweite Review-Runde:**
- *Alle* ursprünglichen Beurteiler reviewen erneut. Auch die, die im ersten Lauf passten.
- Begründung: ein überarbeiteter Plan ist ein neuer Plan. Wer vorher passte, muss prüfen ob seine Bedenken durch die Überarbeitung neu entstanden sind.

**Zweiter Block:**
- Mindestens ein Beurteiler liefert wieder `blocking: true` → Pipeline endet.
- Plan, akkumulierte Notizen aus beiden Runden, Begründung der Eskalation gehen ans Ticket.
- Mensch entscheidet: Ticket umformulieren, manuell überarbeiten, schließen.

**Was nicht passiert:** dritte Runde, automatische Skill-Auswahl-Änderung, manuelle Pipeline-Verlängerung. Eskalation an den Menschen ist die einzige Route.

---

## Plan- und Diff-Persistenz

**Plan-Persistenz:**
- Plan ist nicht nur In-Memory-Zwischenschritt. Er wird in den Pipeline-Run als persistierter Output geschrieben.
- Form (Datei, Pipeline-Storage, beides) ist Phase-C-Entscheidung. Kriterium für die Wahl: Replay eines Pipeline-Runs muss möglich sein (nochmal mit demselben Input laufen lassen), CI-Integration muss den Plan vor Implementation extrahieren können, Debug muss den Plan im Klartext lesen können.
- Bei Block kommt der Plan ins Ticket-Writeback (mit Notizen).
- Bei Pass wandert der Plan zum Implementation-Skill und bleibt im Run-Ergebnis.

**Diff-Persistenz:**
- Diff wird in den Pipeline-Run geschrieben.
- Im Ticket-Writeback verlinkt auf den Branch oder PR (nicht der Diff selbst — das wird zu groß für Tickets).

**Sichtbarkeit zwischen Phasen — opt-in:**
- Default ist *kein Datenfluss*. Eine Phase sieht den Output der vorhergehenden Phase nur, wenn die Pipeline-Implementation es explizit deklariert.
- Pipeline-Implementationen deklarieren Datenflüsse als gerichtete Kanten: `Plan → Review`, `Plan → Implementation`, `Implementation → Verify`, `Investigate → Filter`, `Filter → Synthesize`.
- Skills können *parallele-Phase-Outputs* nicht lesen — nicht nur per Default, sondern strukturell (siehe Skill-Sequencing).
- Zugriff auf Run-State-Daten außerhalb der deklarierten Datenflüsse wird hart abgelehnt.

**Konzepte sind global lesbar** für Aktivierungs-Evaluation. Der opt-in-Datenfluss gilt nur für Run-State-Daten (Plan, Diff, Observations, Tool-Outputs, Ticket-Text), nicht für Konzepte. Sonst könnten Skills ihre Aktivierungsbedingung nicht prüfen.

**Begründung für opt-in:** Datenflüsse sind in der Pipeline-Implementation als gerichtete Übergaben sichtbar — kein YAML-Parallel-Universum, keine impliziten Übergaben. Ein Mensch liest den Pipeline-Code und weiß genau welche Phase was sieht.

---

## Konfiguration: globale Datei

**Eine Datei: `agentsmith.yml`** (Name vorläufig, finale Wahl in Implementation).

Inhalt:

```yaml
limits:
  max_tool_calls_per_skill: 30
  max_tool_calls_per_investigator: 10
  max_tool_calls_per_verifier: 20
  max_llm_calls_per_skill: 15
  max_input_tokens_per_skill_call: 200_000
  max_output_tokens_per_skill_call: 16_000
  max_seconds_per_skill_call: 300
  max_concurrent_skill_calls: 10

pre_filters_path: pre-filters.yaml
concept_vocabulary_path: concepts.yaml

clustering:
  max_findings_per_cluster: 5
  second_perspective_confidence_threshold: 60
```

**Was ausdrücklich nicht in dieser Datei steht:** Pipeline-Definitionen, Skill-Auswahl-Regeln, projekt-spezifische Tuning-Werte. Pipelines werden im Code als Command-Sequenzen implementiert (siehe Phase C, Sektion 3) — keine deklarative YAML-Datei. Skills haben ihre Aktivierungsbedingung im Frontmatter. Projekte werden nicht durch Konfig-Override individualisiert — wer Sonderbehandlung braucht, schreibt eine eigene Pipeline.

---

## Was Phase B nicht abdeckt

- **Skill-Body-Format** und Skill-Frontmatter-Felder im Detail — Phase C.
- **Plan- und Diff-JSON-Schemas** im Detail — Phase C.
- **Ticket-Writeback-Format** (wie Eskalations-Output ans Ticket geht) — Phase D oder E.
- **Migrations-Pfad** vom heutigen Code (Multi-Body-Skills, `roles_supported`, `activation.positive`) zum neuen Modell — Phase G.
- **Konkrete Pipeline-Implementations** (welche Skills welche Pipeline benutzt) — Phase C.

---

## Erfolgskriterium

Ein Mensch liest Phase B und kann skizzieren:
1. Was passiert mechanisch beim Skill-Call eines Untersuchers (Tool-Loop, Limits, Aktivierungs-Bereich).
2. Wie die Triage einen Skill ein- oder ausschaltet (Aktivierungs-Ausdruck über Konzepte).
3. Wie n Beurteiler in der Review-Phase laufen (parallel, ohne geteilten Kontext).
4. Was bei einem Block passiert (Aggregation aller Notizen, Re-Plan, zweite Review-Runde, Eskalation).
5. Wo Loop-Limits konfigurierbar sind (eine Datei, global).

Wenn nicht — zurück, nicht weiter.