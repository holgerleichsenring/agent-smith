# Coding principles — web

- A name says what the thing is, not what layer it sits in.
- A rule the tests can check lives in a function, not in a comment.
- `any` is not a type; the lint stage refuses it.
- A change is finished when `npm run build`, `npm run lint` and `npm test` are green —
  the three stages this context declares, in that order.
