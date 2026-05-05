# Git hooks for this repository

These hooks block accidental direct commits and pushes to `main`.

## Activate (one-time, per clone)

```pwsh
git config core.hooksPath .githooks
```

That's it — the hooks live in this folder and run from here.

## What they do

- **pre-commit** — refuses `git commit` while `HEAD` is `main`.
- **pre-push** — refuses any push whose remote ref is `refs/heads/main`.

## Bypass (emergencies)

```pwsh
git commit --no-verify
git push --no-verify
```

## Workflow

```pwsh
git switch -c my-change
# ...edit, commit...
git push -u origin my-change
# open PR on GitHub, merge, then locally:
git switch main
git pull --ff-only
git branch -d my-change
```
