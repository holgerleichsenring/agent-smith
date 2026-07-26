---
name: feedback_read_deployed_skill_not_worktree
description: "Reason about live agent behavior from the DEPLOYED/committed skill, not the working-tree file"
metadata:
  type: feedback
status: proposed
---
When diagnosing why the live agent behaved a certain way (e.g. wrote a bare
`.agentsmith/plan.md` instead of `{RunRecordDir}/plan.md`), read the **deployed/
committed** skill, not the working-tree `SKILL.md`. The agent-smith-skills repo
had an UNCOMMITTED `coding-agent-master` 1.7.0 ({RunRecordDir}) in the working
tree, but the deployed skill was committed 1.6.0 (bare path). I read the working
tree, concluded "the skill already has it, the model ignored it" — wrong: the
agent correctly followed the live 1.6.0; the fix existed but was never committed/
deployed (a deployment gap, not model deviation).

**Why:** skills are pinned/bind-mounted (see [[feedback_deploy_server_image]]);
the running version is `git show HEAD:…/SKILL.md` or the release pin, not the
local file. A working-tree edit someone left uncommitted misleads the diagnosis.

**How to apply:** before claiming "the skill says X" about a live run, run
`git show HEAD:<skill path>` (and check the deployed pin/bind-mount version);
compare against the working tree. If they differ, the uncommitted delta is the
real story. Mirrors [[feedback_used_to_work_check_history]] — check what is
actually deployed, not what is locally present.
