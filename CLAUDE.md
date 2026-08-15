# Working in this repo

## CHANGELOG.md and releases

`CHANGELOG.md` and GitHub releases (tagged `v<version>`, built by
`.github/workflows/latest.yml`) track **application code changes only** —
i.e. a push that touches `PolicyPlus/`.

Workflow files, documentation, `CHANGELOG.md` itself, and other repo-admin changes do
**not** get a changelog entry and do **not** bump the version number. They live in commit
history only — write a clear commit message, nothing more.

Only bump the version (`## [X.Y]` header at the top of `CHANGELOG.md`) and write a new
entry when a push actually changes something under `PolicyPlus/`. Run `version.bat`
afterward to sync that number into `AssemblyInfo.cs`/`Version.cs` before committing.
