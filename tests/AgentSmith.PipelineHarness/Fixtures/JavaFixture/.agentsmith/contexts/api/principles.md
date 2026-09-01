# Coding principles — api

- A name says what the thing is, not what layer it sits in.
- A rule the tests can check lives in a method, not in a comment.
- Money is counted in whole units; a rounding decision is made once and named.
- A change is finished when `mvn -B -DskipTests package` and `mvn -B test` are green —
  the two stages this context declares, in that order.
