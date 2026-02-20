# AGENT_WORKFLOW.md

Standard workflow for all dev agents and humans.

## 1) Start of work (always)

1. `git fetch --all --prune`
2. `git checkout <default-branch>` (usually `main`)
3. `git pull --rebase origin <default-branch>`
4. Confirm clean base: `git status`

Never start new implementation work from a stale branch.

## 2) Issue-first execution

- Every task starts from `owen-task.yml`
- Required: objective, acceptance criteria, spec-ready checklist, expected files, test plan, doc impact, rollback plan
- If spec is ambiguous, stop and escalate before coding.

## 3) Branching rules

- One branch per task
- Naming:
  - `agent/<owner>/<short-task>`
  - `hotfix/<short-task>` for urgent production fixes

## 4) Development + validation

- Follow issue scope and constraints exactly
- Run test plan before opening PR
- If blocked, report: blocker / workaround / exact ask / parallel work

## 5) Pull request requirements

Use PR template and include:
- Summary / what changed
- Validation evidence
- Doc impact
- Risk assessment
- Rollback plan

## 6) Merge policy

- No direct commits to default branch
- Required checks must pass
- Squash merge preferred
- Delete branch after merge

## 7) Safety rules

- Never push secrets
- Never bypass branch protection
- Keep changes scoped to issue
- Keep docs in sync with behavior changes
