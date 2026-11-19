# 2026-11-18-9c1cd: master tool surfaces

Every master offers at most 20 tools, no two doing one job, and the set is fixed for the whole run or
conversation: a tool that cannot act this turn is still offered and answers why, in words the agent can act on.
Brackets mark tools that a project's CONFIG decides (stable for a run and a conversation).

## Coding master (coding-agent-master), at most 20

| tool | what it does | main parameters |
|---|---|---|
| read_file | reads a file; an image of a carried upload (`.agentsmith/reference/<set>/…png`) is shown as a picture | path, start_line?, line_count?, with_line_numbers? |
| write_file | writes a whole file (context.yaml is refused: write_context_yaml) | path, content |
| write_context_yaml | writes a context.yaml from its JSON document, checked and serialised | repo, context_name, document |
| edit | one exact replacement, or several applied atomically | path, old_string?, new_string?, replace_all?, edits?[{old_string,new_string,replace_all}], dry_run? |
| grep_in_tree | regex search under a directory or in one file | pattern, root? (dir or file), glob?, output_mode?, context? |
| find_files | glob match on paths | pattern, root?, head_limit? |
| directory_tree | nested listing; max_depth 1 = direct children; with_sizes / sort_by give the flat listing | root?, max_depth?, exclude_globs?, with_sizes?, sort_by? |
| run_command | shell in a repo's sandbox; or `stage` runs a verify stage a context declared, in that context's own sandbox | command? \| stage + context?, repo?, timeout_seconds? |
| web_fetch | GET a public URL as markdown/raw; a Figma link with node-id is read through the project's Figma source | url, max_length?, start_index?, raw?, source?, depth?, expected_version?, read_version? |
| [render_reference] | renders a page in the browser (sandbox.browser.enabled); with `reference` + `pairs` compares `source` against it | source, selectors?, reference?, pairs?, viewport? |
| log_decision | records a decision; `overrulable: true` records a default a person may overrule later (needs question, alternatives, why) | category, decision, repository?, overrulable?, question?, alternatives?, why?, holds_series? |
| ask_human | asks the operator | question, context?, choices?, recommended_index? |
| recall | searches stored memories | query |
| remember | records/updates a memory; `dispute` names memory `name` as having led you wrong | type?, name, description?, body?, repository?, dispute? |
| ensure_repo_sandbox | brings another configured repo into the run | repo_name |
| update_progress | the run's checklist | items |
| revise_spec | (spec runs) rewrites the spec | spec_yaml, reason |
| finish_spec | (spec runs) ends the spec, with the build note for the next spec | status, summary, failing_tests?, baseline_failing_tests?, follow_ups?, blocker?, ignored_instructions?, renamed?, moved?, persisted?, deviations?, relied_on? |
| spawn_agents | starts sub-agents (background or waiting) | tasks, background? |
| send_message | writes to a sub-agent; `wait: true` (no agent_id) waits for the next one to finish | agent_id?, text?, wait? |

## Design partner (design-partner-master), unbound 17, ticket-bound 19

| tool | what it does | main parameters |
|---|---|---|
| read_file | reads a repo/template/upload file; `draft:<path>`, `mock:` (list) / `mock:<spec_id>[/v<n>]`, `notes:`; an image in an upload (`reference:<name>/<file>.png`) is shown as a picture | path, start_line?, line_count? |
| grep_in_tree | as above | pattern, root?, glob? |
| find_files | as above | pattern, root? |
| directory_tree | as above (also `draft:`) | root?, max_depth? |
| web_fetch | as above | url |
| render_reference | renders a page; with `reference` + `pairs` compares | source, selectors?, reference?, pairs?, viewport? |
| run_command | shell in your work clone of `repo`, or in an upload's container when repo is `reference:<name>`; answers why when not allowed (poster lacks runs.control, in-process runtime, shared bus) | command, repo, history?, timeout_seconds? |
| write_file | `draft:<path>` writes a draft file (checked); `mock:<spec_id>` stores the next mock version and renders it; `notes:` replaces the conversation notes; `delete: true` deletes a draft file or directory | path, content?, delete? |
| edit_file | exact replacement in a `draft:` file or in `notes:` | path, old_string, new_string, replace_all? |
| state_work_picture | records the work picture and/or the open questions for the draft | kind?, truth?, size?, effort?, open_questions?[{question,proposal,specIds}] |
| offer_filing | offers the operator to file the draft as it stands | line |
| name_conversation | names an unnamed conversation; refuses when it already has a name | subject |
| recall | as above; answers that the project keeps no memories yet when none exist | query |
| remember | keeps a project memory; `dispute` disputes memory `name`; answers why when the poster may not | type?, name, description?, body?, dispute? |
| read_ticket | (bound) the ticket: part `text` (default, from), `tracker` (linked PRs/branches), `runs` (this framework's runs), `series` (verify: true reviews it against the code) | part?, from?, verify? |
| start_next_attempt | (bound) on the person's word: mode `attempt` (default), `derive` (specs on the branch, no attempt), `preview`; answers why when the poster may not / no attempt | reason, mode? |
| withdraw_filed_ticket | closes a ticket this conversation filed; answers that nothing was filed yet | key, reason? |
| spawn_agents | as above | tasks, background? |
| send_message | as above | agent_id?, text?, wait? |

## Ticket steward (ticket-steward-master), 19

read_file, grep_in_tree, find_files, directory_tree, web_fetch, render_reference, run_command,
write_file (`notes:` only; draft and mock paths are refused: the steward changes the series with edit_series),
edit_file (`notes:` only), name_conversation, recall, remember, read_ticket (as above, series included),
edit_series (edits[{kind, specId, yaml}]; answers why when the poster may not), start_next_attempt (reason, mode?),
read_run_events (runId, type?, step?, afterSeq?, limit?), read_config, spawn_agents, send_message.

## Children

- coding child (13): read_file, write_file, edit, grep_in_tree, find_files, directory_tree, run_command, web_fetch, write_context_yaml, log_decision (no overrulable), ask_human, recall, remember (no dispute).
- design child (7): read_file, grep_in_tree, find_files, directory_tree, web_fetch, recall, read_ticket (bound; no series part).
- scan master: read_file, grep_in_tree, find_files, directory_tree, http_request, log_decision, web_fetch, recall, remember (+dispute), spawn_agents, send_message.

## Old name -> new name / gone

| old | new |
|---|---|
| multi_edit | edit (edits) |
| grep_in_file | grep_in_tree (root = the file) |
| list_directory | directory_tree (max_depth 1) |
| http_request (coding master) | run_command (curl); kept on scan masters |
| get_artifact_credentials | gone: registry auth is staged before the master (SetupRegistryAuth) and in a sandbox ensure_repo_sandbox adds |
| design_read | web_fetch on the Figma link (source, depth, expected_version, read_version) |
| decide_by_default | log_decision (overrulable: true, question, alternatives, why, holds_series) |
| dispute_memory | remember (name = slug, dispute = reason) |
| hand_forward | finish_spec (renamed, moved, persisted, deviations, relied_on) |
| run_gate_stage | run_command (stage, context, repo) |
| wait_for_agents | send_message (wait: true) |
| compare_reference | render_reference (reference, pairs, viewport) |
| view_reference_image | read_file on the image's path |
| run_in_reference | run_command (repo: reference:<name>) |
| write_mock | write_file (path mock:<spec_id>) |
| read_mock | read_file (mock: / mock:<spec_id>[/v<n>]) |
| read_notes | read_file (notes:); the prompt carries the notes each turn |
| write_notes | write_file / edit_file (notes:) |
| delete_file | write_file (delete: true) |
| state_open_questions | state_work_picture (open_questions) |
| ticket_work | read_ticket (part: tracker) |
| ticket_runs | read_ticket (part: runs) |
| read_series | read_ticket (part: series) |
| verify_series | read_ticket (part: series, verify: true) |
| derive_specs | start_next_attempt (mode: derive / preview) |
