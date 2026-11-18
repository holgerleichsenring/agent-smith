# design-partner-master: sentences to change after 2026-11-18-9c1ca

Read against agent-smith-skills origin/main 3bde696 (5.37.1),
`skills/_masters/design-partner-master/SKILL.md`. With 9c1ca, `review_proposal`
is gone, nothing reviews the draft after the turn, and nothing sends the master
back with a report.

## Lines 124-126 (revision paragraph)

Now: "...edits the files it touches in place and leaves the rest standing, so
what the reviewers already read stays read."

Replace with: "...edits the files it touches in place and leaves the rest
standing."

## Lines 133-140 (review paragraph)

Now: "`review_proposal` gives you a reviewer's read of the draft: fresh
reviewers who did not write it read it against the code and the operator's own
words, and find what an author misses in their own cut. On a proposal of
several items, a second look before you finish is worth its minutes. A text no
reviewer has read when you finish is reviewed then, and its report comes back
to you before your reply goes out. Weigh what the reviewers find, change what
convinces you, and say in your reply which objections still stand and why."

Replace with: "You own the draft; nothing reviews it after you finish. When you
want a second read, and on a proposal of several items it is worth its minutes,
start a sub-agent with `spawn_agents` and a review brief: attack this spec
before anyone builds it, check every step against the code as it is and the
operator's own words, and answer BUILD, BUILD WITH CUTS or REFUSE with
file:line evidence. Give it the whole draft, so it judges each spec beside its
siblings and a capability another ticket carries is not read as dropped. Weigh
what comes back, change what convinces you, and say in your reply which
objections still stand and why."

## Line 201 (ticket bullet)

Now: "its specs under `draft:tickets/<key>/` are written like any spec,
reviewed in this turn, and filed onto the ticket's branch"

Replace with: "its specs under `draft:tickets/<key>/` are written like any spec
and filed onto the ticket's branch"
