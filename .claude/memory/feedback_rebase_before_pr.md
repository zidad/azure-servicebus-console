---
name: Rebase on main before pushing PRs
description: Always fetch main and rebase the feature branch on top of it before pushing and opening a PR
type: feedback
---

Always run `git fetch origin && git rebase origin/main` on the feature branch before pushing and creating a PR.

**Why:** Avoids merge conflicts in PRs and keeps the branch history clean on top of main.

**How to apply:** Any time a new branch is ready to push, or before calling `gh pr create`, run the fetch+rebase first. If conflicts arise, resolve them before pushing.
