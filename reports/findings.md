## Agent Smith Security Review

Found **30** issues (11 critical, 11 high, 8 medium, 0 low)

### 🔴 CRITICAL: Slack Token Hardcoded in .env File

**Location:** `.env:3`

Highly sensitive Slack token present in .env and confirmed by multiple sources. This poses a severe risk if leaked and enables account takeover, privilege escalation. Must be revoked/rotated and removed from source.

### 🔴 CRITICAL: GitHub Personal Access Token Hardcoded in .env File

**Location:** `.env:6`

Confirmed by multiple independent sources, the GitHub PAT enables direct code access and privilege escalation if leaked. This token should be rotated and never kept in code.

### 🔴 CRITICAL: Slack Token Embedded in HTML

**Location:** `site/deployment/chat-gateway/index.html:2754`

Static HTML contains a real Slack token, exposing credentials to any visitor/crawler. This is a critical exposure and must be remediated immediately.

### 🔴 CRITICAL: Slack Token Present in Search Index

**Location:** `site/search/search_index.json:1`

Search index contains a Slack token and SQL injection patterns, posing both credential exposure and injection risk. File can be scraped, and must be remediated.

### 🔴 CRITICAL: Slack Token Embedded in HTML

**Location:** `site/slack-setup/index.html:2716`

Slack token in static HTML is a high-severity exposure, enabling takeover of the bot/app if ever served to end users.

### 🟠 HIGH: Slack Token Hardcoded in Test Code

**Location:** `tests/AgentSmith.Tests/Dispatcher/SlackTypedQuestionTests.cs:225`

Token in test code is confirmed valid or realistic, risk of accidental leak remains even if in testing context. Must use placeholders, never live tokens, in all source and tests.

### 🔴 CRITICAL: RSA Private Key in Version-Controlled File

**Location:** `config/patterns/secrets.yaml:124`

Multiple sources confirm the presence of an unencrypted RSA private key in VCS. This enables impersonation, data decryption, or signing. Must be removed and rotated, never present in repo.

### 🔴 CRITICAL: SSH Private Key Present in Source

**Location:** `config/patterns/secrets.yaml:132`

Exposure of SSH private key in repository is confirmed and critical. Remote access or privilege escalation is possible if host still recognizes this key.

### 🔴 CRITICAL: Private Key Present in Configuration Patterns

**Location:** `config/patterns/secrets.yaml:140`

Generic private key exposure in config files, confirmed by multiple sources. This is critical for all environments and must be remediated with rotations.

### 🟡 MEDIUM: Placeholder Secret (CHANGE_ME) Found in Configuration

**Location:** `config/patterns/secrets.yaml:188`

CHANGE_ME placeholder present in shipped config. Automated exploitation tools look for such credentials. Replace pre-release and ensure secrets are set in deployment.

### 🟡 MEDIUM: Placeholder Secret (CHANGE_ME) Found in Configuration

**Location:** `config/patterns/secrets.yaml:192`

Repeated placeholder secret in config. While lower severity than live tokens, must be removed and replaced before deploying.

### 🟡 MEDIUM: Placeholder Secret (CHANGE_ME) Found in Configuration

**Location:** `config/patterns/secrets.yaml:193`

Multiple placeholder secrets present. These can be exploited if left in any production environment or copied into downstream setups.

### 🟡 MEDIUM: Placeholder Secret (CHANGE_ME) Found in Configuration

**Location:** `config/patterns/secrets.yaml:201`

Same as above—CHANGE_ME is a common attack word for credential spraying in CI/CD and SaaS platforms.

### 🟠 HIGH: CORS Wildcard Origin Configuration

**Location:** `config/patterns/config.yaml:76`

Wildcard CORS setting (“*”) confirmed by multiple sources as a high-severity misconfig, exposing API endpoints to cross-origin abuse and client-side credential theft.

### 🟠 HIGH: TLS Certificate Verification Disabled (NODE_TLS_REJECT_UNAUTHORIZED=0)

**Location:** `config/patterns/config.yaml:97`

TLS certificate checks disabled, confirmed in config and audit findings. Enables MITM attacks on Node.js clients. Apply production configuration and review all environment usage.

### 🟠 HIGH: TLS rejectUnauthorized Disabled

**Location:** `config/patterns/config.yaml:105`

TLS client-side verification is explicitly set to false, exposing outbound connections to spoofing and interception. Must always be true or omitted for production.

### 🟡 MEDIUM: Kubernetes Container Without Resource Limits (server)

**Location:** `deploy/k8s/base/server/deployment.yaml:26`

Missing resource limits in server container is a well-known denial-of-service risk, confirmed by all config experts and scanners.

### 🟡 MEDIUM: Kubernetes Container Without Resource Limits (redis)

**Location:** `deploy/k8s/base/redis/deployment.yaml:20`

Redis deployment lacking CPU/memory limits is confirmed and exposes entire cluster to stability and DoS risk.

### 🟡 MEDIUM: Kubernetes Container Without Resource Limits (server, dev environment)

**Location:** `deploy/k8s/overlays/dev/patch-server-dev.yaml:10`

While in dev overlay, controls should still be present to prevent runaway processes. Confirmed as existing risk.

### 🟡 MEDIUM: Kubernetes Container Without Resource Limits (server, prod environment)

**Location:** `deploy/k8s/overlays/prod/patch-server-prod.yaml:10`

Production overlay missing resource limits for containers is a validated resource exhaustion risk and should be fixed.

### 🔴 CRITICAL: SQL Injection via String Concatenation or Template Literals

**Location:** `site/assets/javascripts/bundle.79ae519e.min.js.map:4`

Multiple scanners confirm SQL statements are constructed with string concatenation/template literals directly involving user-influenced data, opening up classic SQL injection vectors.

### 🔴 CRITICAL: SQL Injection via String Concatenation or Template Literals in Search Worker

**Location:** `site/assets/javascripts/workers/search.2c215733.min.js.map:4`

Same pattern as above; worker carries SQLi risk due to dynamic SQL assembly. Parameterization and sanitization required.

### 🔴 CRITICAL: SQL Injection via Template Literal in Search Index

**Location:** `site/search/search_index.json:1`

Template literal construction of SQL statements in a public index file is highly dangerous and could expose backend DB to injection from manipulated or untrusted input.

### 🟠 HIGH: Shell Command Injection Risk: shell: true in pipeline

**Location:** `config/patterns/injection.yaml:41`

shell: true use confirmed by code scanners, enabling OS command injection from user-passed variables unless strictly sanitized. Input validation strongly recommended.

### 🟠 HIGH: dangerouslySetInnerHTML Usage in Configuration

**Location:** `config/patterns/injection.yaml:44`

Use of dangerouslySetInnerHTML is a high XSS risk, validated by multiple tools. All content must be strictly sanitized prior to rendering via this method.

### 🟠 HIGH: Server-Side Template Injection: dangerouslySetInnerHTML Usage

**Location:** `config/patterns/injection.yaml:48`

Another occurrence (per config and injection finding); validates systemic template injection/XSS exposure if user/LLM content is ever rendered via these pathways.

### 🟠 HIGH: Server-Side Template Injection: dangerouslySetInnerHTML Usage

**Location:** `config/patterns/injection.yaml:49`

Repeated use means XSS risk is broad and not compartmentalized. All usages must be audited for safe content flow.

### 🟠 HIGH: Python Pickle.load() Usage: Arbitrary Code Execution

**Location:** `config/patterns/injection.yaml:96`

pickle.load(s) pattern is confirmed dangerous unless limited to strictly internal or trusted serialized data. Arbitrary code execution risk if user content reaches this API.

### 🟠 HIGH: PHP unserialize() Usage: Code Execution/Injection

**Location:** `config/patterns/injection.yaml:112`

PHP unserialize() on untrusted input is a well-known RCE vector. Exclusion only possible if input is 100% from system-trusted sources, which cannot be guaranteed here.

### 🟠 HIGH: Server-Side Template Injection: dangerouslySetInnerHTML Usage (AI Security)

**Location:** `config/patterns/ai-security.yaml:68`

dangerouslySetInnerHTML rendering AI-generated (LLM) content is doubly risky due to prompt injection/XSS chain. All such content must be HTML-sanitized.

