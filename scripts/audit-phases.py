"""
Audit done phases for spec-vs-reality drift.
For each phase, extracts claimed file paths (new:/modify:) and test names (tests:),
checks they exist in the repo. Surfaces 'deferred' markers as known gaps.
"""
import os, re, glob, sys
from collections import defaultdict

ROOT = '/Users/da5id/develop/source/others/agent-smith/agent-smith'
PHASES = sorted(glob.glob(os.path.join(ROOT, '.agentsmith/phases/done/*.yaml')))

# Walk repo once, build file index
SRC_INDEX = set()
for d in ['src', 'tests', 'docs', 'config', 'skills', 'deploy', '.github']:
    for root, _, files in os.walk(os.path.join(ROOT, d)):
        for f in files:
            full = os.path.relpath(os.path.join(root, f), ROOT)
            SRC_INDEX.add(full)
            SRC_INDEX.add(full.split('/')[-1])  # also basename

# Find all test method names declared with [Fact]/[Theory] in tests
TEST_METHODS = set()
for cs_file in glob.glob(os.path.join(ROOT, 'tests/**/*.cs'), recursive=True):
    with open(cs_file) as fp:
        content = fp.read()
    for m in re.finditer(r'public\s+(?:async\s+)?(?:Task\s+|void\s+)([A-Z]\w+)\s*\(', content):
        TEST_METHODS.add(m.group(1))

# Patterns
PATH_RE = re.compile(r'(?:^|\s|")([\w\-./]+\.(?:cs|md|yml|yaml|json|sh|cshtml|csproj))', re.MULTILINE)
TEST_NAME_RE = re.compile(r'^\s*-\s*"?([A-Z]\w+_\w+(?:_\w+)+)"?', re.MULTILINE)
DEFERRED_RE = re.compile(r'(?i)\b(deferred|TODO\b|placeholder|stub|silently\s+(?:fails|registers))', re.MULTILINE)

drift_report = defaultdict(lambda: {'missing_files': [], 'missing_tests': [], 'deferred': []})

for phase_path in PHASES:
    phase_name = os.path.basename(phase_path).replace('.yaml', '')
    with open(phase_path) as fp:
        content = fp.read()

    # File-path claims under new:/modify: (both for src and tests)
    for m in PATH_RE.finditer(content):
        path = m.group(1)
        # Filter: only relevant repo paths
        if not (path.startswith(('src/', 'tests/', 'docs/', 'config/', 'skills/', 'deploy/', '.github/'))
                or '/' in path and any(path.endswith(ext) for ext in ['.cs', '.md', '.yml', '.yaml'])):
            continue
        # Skip URLs, paths with placeholders
        if '://' in path or '{' in path or '...' in path:
            continue
        if path not in SRC_INDEX:
            # try just basename match
            if os.path.basename(path) not in SRC_INDEX:
                drift_report[phase_name]['missing_files'].append(path)

    # Test method claims under tests:
    in_tests = False
    for line in content.splitlines():
        if line.startswith('tests:'):
            in_tests = True; continue
        if in_tests and line and not line.startswith((' ', '#', '-')):
            in_tests = False
        if in_tests:
            tm = TEST_NAME_RE.match(line)
            if tm:
                tname = tm.group(1)
                # the spec sometimes appends "(comment)" — drop everything after a space inside the literal
                tname_core = tname.split()[0]
                if tname_core not in TEST_METHODS:
                    drift_report[phase_name]['missing_tests'].append(tname_core)

    # Deferred markers — explicit known gaps
    for m in DEFERRED_RE.finditer(content):
        # grab line context
        line_start = content.rfind('\n', 0, m.start()) + 1
        line_end = content.find('\n', m.end())
        line = content[line_start:line_end if line_end > 0 else len(content)].strip()
        if len(line) > 200: line = line[:197] + '...'
        drift_report[phase_name]['deferred'].append(line)

# Summarize
print(f"Audit of {len(PHASES)} done phases\n" + "=" * 60)

phases_with_missing_files = [p for p, d in drift_report.items() if d['missing_files']]
phases_with_missing_tests = [p for p, d in drift_report.items() if d['missing_tests']]
phases_with_deferred = [p for p, d in drift_report.items() if d['deferred']]

print(f"\nMISSING FILES (spec promised, not found in repo):")
print(f"  {len(phases_with_missing_files)} phases affected")
for p in sorted(phases_with_missing_files)[:20]:
    print(f"\n  {p}:")
    for f in drift_report[p]['missing_files'][:5]:
        print(f"    - {f}")

print(f"\n\nMISSING TESTS (spec listed, not found as test method):")
print(f"  {len(phases_with_missing_tests)} phases affected")
for p in sorted(phases_with_missing_tests)[:20]:
    print(f"\n  {p}:")
    for t in drift_report[p]['missing_tests'][:5]:
        print(f"    - {t}")

print(f"\n\nDEFERRED MARKERS (explicit known gaps):")
print(f"  {len(phases_with_deferred)} phases affected")
for p in sorted(phases_with_deferred):
    print(f"\n  {p}:")
    for line in drift_report[p]['deferred'][:3]:
        print(f"    {line}")
