---
name: feedback_dont_touch_operator_runtime
description: "Never stop/kill/restart the operator's running containers or services unilaterally — they manage their runtime"
metadata:
  type: feedback
status: proposed
---
Don't `docker stop`/`rm`/restart the operator's running containers, server, or services on your own — even when something is misbehaving (a runaway loop, a swarm). They are actively driving their own runtime and may be mid-investigation (e.g. killing containers individually on purpose). On 2026-06-06 I ran `docker stop deploy-server-1` + removed sandboxes to halt a re-trigger loop; the operator was managing it themselves and called it "sinnlos / übergriffig."

**Why:** their local/prod runtime is theirs; an unrequested stop destroys in-flight state they may be examining and overrides their control.

**How to apply:** diagnose and READ (logs, `docker inspect`, redis-cli) freely — those are non-destructive. For anything that stops/removes/restarts a container or service: propose the exact command and let them run it (or wait for an explicit "do it"). Offering is fine; executing without a clear go is not. Related: [[feedback_verify_via_harness]].
