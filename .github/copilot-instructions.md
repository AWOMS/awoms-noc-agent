# Copilot Agent Instructions

## Conventional Commits Are Mandatory
- Every commit **must** follow the Conventional Commits spec.
- Use the format `<type>(<scope>): <short description>`.
- Approved `type` values: `feat`, `fix`, `chore`, `docs`, `refactor`, `perf`, `test`, `build`, `ci`, `revert`.
- `scope` should point to the primary area touched (e.g., `agent`, `functions`, `shared`, `docs`). If uncertain, default to `core`.
- Keep the description under 60 characters, start with a verb in present tense, and avoid punctuation at the end.

## Commit Hygiene Rules
- Split unrelated changes into separate commits so each message represents a single logical unit of work.
- Never use merge commits or auto-generated messages; always craft the message manually per the rules above.
- If a change does not warrant a code modification (e.g., version bump only), still use the appropriate type/scope (e.g., `chore(agent): bump version`).

## Validation Steps Before Committing
1. Ensure the change passes existing tests or linters relevant to the touched project(s).
2. Confirm the commit message is Conventional-Commit compliant before running `git commit`.
3. If multiple logical changes were made, interactively stage and commit them separately to keep history clean.
