# Apache Spark Overlay

<!-- agentsmith:principles-overlay spark v1 -->

## Detection

```yaml
- file: build.sbt
  contains: org.apache.spark
- file: pom.xml
  contains: org.apache.spark
- file: build.gradle
  contains: org.apache.spark
- file: build.gradle.kts
  contains: org.apache.spark
- file: gradle/libs.versions.toml
  contains: org.apache.spark
- file: requirements*.txt
  contains: pyspark
- file: requirements*.txt
  contains: databricks-connect
- file: pyproject.toml
  contains: pyspark
- file: pyproject.toml
  contains: databricks-connect
- file: setup.py
  contains: pyspark
- file: setup.cfg
  contains: pyspark
```

A signal is a text match: a manifest that names Spark only to exclude it
still applies this overlay.

## Rules

### All languages

- Never assign to a driver-side variable or mutate a driver-side object
  inside a function passed to a distributed operation (`map`, `foreach`,
  `filter`, a UDF): Spark defines that behaviour as undefined. Aggregate
  across executors with an accumulator instead.
- A value that must be exact is accumulated only inside an action — updates
  made inside a transformation may be applied more than once.
- A function passed to `reduce` (or `fold`, `aggregate`) is commutative and
  associative; Spark combines partial results in any order.
- A broadcast value is never modified after it is broadcast.
- `SparkContext` and `SparkSession` are created and used on the driver only —
  never inside a closure or UDF — and one `SparkContext` is active per JVM.
- A UDF whose result is not a pure function of its input is marked
  non-deterministic (`asNondeterministic`); Spark otherwise treats it as
  deterministic and may evaluate it once or several times.
- Every window aggregate states its frame (`rowsBetween` / `rangeBetween`) —
  Spark derives the default frame from whether the window is ordered. The
  functions whose frame Spark fixes take none: `row_number`, `rank`,
  `dense_rank`, `ntile`, `percent_rank`, `cume_dist`, `lag`, `lead`.

### python

- An empty placeholder column is `F.lit(None)`, never `''` or `'NA'` — a
  string is a value, not a missing one.

## Artefacts

No artefacts — Spark's rules are about how code runs on a cluster, which no
configuration file the stack keeps can record.
