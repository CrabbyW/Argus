# AI Instructions

These instructions define how the AI assistant should behave during development tasks, with a focus on planning, implementation tracking, and file organization.

---

## General Behavior

- Always prefer clarity and precision over brevity when making decisions that affect the codebase.
- Ask for clarification before making assumptions on ambiguous requirements.
- Never delete or overwrite existing work without explicit confirmation.
- Prefer incremental, reviewable changes over large sweeping modifications.

---

## Planning

### When to Create a Plan

Create a plan file **before** starting any task that involves:

- More than one file being created or modified.
- A new feature, module, or architectural change.
- A refactor that touches multiple components.
- Any work that will span multiple sessions or steps.

For simple, single-step tasks (e.g., fix a typo, rename a variable), a plan file is **not required**.

---

### Plan File Location

All plan files **must** be stored under:

```
ingenium/ai-plans
```

This folder must exist at the root of the project. If it does not exist, create it before saving any plan.

---

### File Naming Convention

Plan files must follow this naming pattern:

```
ai-implementation-plan/<N>_<snake_case_description>.md
```

**Rules:**

- `<N>` — a sequential integer incremented from the highest existing number in the folder (e.g., if the last file is `20_analyzer_lint_issues_fix.md`, the next is `21_...`).
- `<snake_case_description>` — a concise, lowercase, underscore-separated summary of the task (e.g., `add_user_auth`, `refactor_api_layer`, `migrate_database_schema`).
- No spaces, hyphens, or uppercase letters anywhere in the filename.
- Keep descriptions concise but descriptive enough to identify the task at a glance.
- Always check the existing files in the folder to determine the correct next number before creating a new plan.

**Examples:**

```
ai-implementation-plan/18_startup_performance_optimization.md
ai-implementation-plan/19_alert_dialog_styling_standardization.md
ai-implementation-plan/20_analyzer_lint_issues_fix.md
ai-implementation-plan/21_add_user_authentication.md
```

---

### Plan File Structure

Every plan file must follow this structure:

```markdown
# Plan: <Human-Readable Title>

**Date:** YYYY-MM-DD  
**Status:** `In Progress` | `Complete` | `Blocked`  
**Description:** A brief paragraph explaining the goal of this task and any relevant context.

---

## Checklist

- [ ] Step one description
- [ ] Step two description
- [ ] Step three description
- [ ] ...

---

## Notes

Any assumptions, open questions, risks, or decisions made during planning or implementation.
```

---

### Checklist Rules

- Every discrete unit of work must be represented as a **markdown checkbox**.
- Use the format `- [ ]` for incomplete items and `- [x]` for completed items.
- Checkboxes must be **checked during or immediately after implementation** of that step — not all at the end.
- If a step was partially completed or skipped, add a note inline:
  ```markdown
  - [x] Set up database connection _(used SQLite instead of PostgreSQL — see Notes)_
  - [ ] Write migration scripts _(blocked: schema not finalized)_
  ```
- Do not remove unchecked items. Leave them unchecked and explain in Notes if they were intentionally skipped or deferred.

---

### Progress Tracking

- The `Status` field in the plan header must be kept up to date:
  - `In Progress` — work has started but is not complete.
  - `Complete` — all checklist items are checked and the task is done.
  - `Blocked` — work cannot continue due to an external dependency or decision needed.
- At the end of each session, review the checklist and update any items completed during that session.
- If new steps are discovered during implementation, **add them to the checklist** rather than doing untracked work.

---

### Example Plan File

**Filename:** `ai-implementation-plan/21_add_user_authentication.md`

```markdown
# Plan: Add User Authentication

**Date:** 2026-03-10  
**Status:** `In Progress`  
**Description:** Implement JWT-based user authentication including registration, login,
and protected route middleware. This will be the foundation for all role-based access
control work to follow.

---

## Checklist

- [x] Create `auth` module folder structure
- [x] Implement `/register` endpoint with input validation
- [x] Implement `/login` endpoint and JWT issuance
- [ ] Write `authenticateToken` middleware
- [ ] Apply middleware to protected routes
- [ ] Write unit tests for auth endpoints
- [ ] Update API documentation

---

## Notes

- Decided to use `jsonwebtoken` library over a custom implementation for reliability.
- Token expiry set to 1 hour; refresh token strategy to be handled in a follow-up plan.
- `/login` endpoint complete but tests not yet written — tracked in checklist above.
```

---

## Implementation

- Follow the plan checklist step by step.
- Check off each item (`- [x]`) as soon as it is implemented.
- Do not skip steps without documenting the reason in the Notes section.
- If the scope changes significantly, update the plan (add/remove steps) and note the change.

---

## Summary of Key Rules

| Rule | Detail |
|---|---|
| Plan folder | `ai-implementation-plan/` at project root |
| File naming | `<N>_snake_case_description.md` (N = next sequential number) |
| Checkboxes | Required for every discrete step |
| Check-off timing | Immediately after each step is completed |
| Status field | Must reflect current state at all times |
| Skipped steps | Must remain in list with explanation, not deleted |