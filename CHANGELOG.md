# Changelog

All notable changes, grouped by [Keep a Changelog](https://keepachangelog.com/) category,
each entry linking to its commit for full detail. Version numbers are custom to this fork
(not semver) — upstream's state at fork time is treated as **1.0**, incrementing by **0.1**
per release.

## [1.27] - 2026-08-16

### Changed
- Collapsed the identical `ThemeService.ApplyPersisted()`/`WpfInterop.SetOwner()` prefix
  duplicated across all 37 `PresentDialog` methods into one `WpfInterop.PreparePresented<T>`
  helper. (`846bdd6`)
- De-duplicated an identical Escape-to-close handler across 32 dialog windows into one
  shared `WpfInterop.HandleEscapeToClose` helper. (`d8d9e2e`)
- Replaced `PolicySearch`'s `Microsoft.VisualBasic` dependency (`Strings`, `ControlChars`,
  `LikeOperator`) with plain C# and a small wildcard-to-regex translator; added the first
  test coverage for it in the process. (`8b8c453`)
- Split `AdmxFile.Load`'s ~360-line single method into 10 per-section methods; added the
  first ADMX-parsing test coverage. (`06a3b52`)

### Removed
- Dead WinForms-era `Resources.resx`/`.Designer.cs` and `Views/MainWindow.resx` scaffolding
  left behind by earlier migrations, and an unused `Row.Policy` field in
  `FindResultsWindow`. (`0fa9d16`)

### Fixed
- A handful of correctness bugs from a code review: `ValuePresent` crashed on ADMX enum
  items missing a `<value>` child; `loadOnOffValList` matched raw element names instead of
  local names, silently breaking namespaced ADMX; `Privilege.EnablePrivilege` ignored all
  three Win32 return codes; `AdmxBundle.AddSingleAdmx` built ADML paths with a substring
  `.Replace()` that could corrupt unrelated path segments. Also guards three lookups
  (element inspector, setting editor, filter options) that threw on mismatched/stale
  ADMX+ADML data instead of degrading gracefully. (`9f6ed9a`)
- 16 dialogs missing `WpfInterop.FixSizeToContent()` still had the oversized-dialog symptom
  the WPF-UI migration's fix only reached 3 windows for. (`cf762dd`)
- `MainWindow`'s category-tree selection could re-enter itself and redo work on routine
  navigation; Favorites' listing silently went empty after any workspace reload. (`66d07c4`)

## [1.26] - 2026-08-16

### Changed
- Migrated the entire app from WinForms to WPF-UI (Fluent design) — every window, dialog,
  and popup now shares one consistent look. (`86b993b`, `e17df1e`)
- Import REG now infers the target hive (Computer/User) from the file instead of always
  asking. (`68b82e5`)

### Fixed
- Menu bar spacing, oversized popups, and invisible list/tree text — several rounds of
  polish following the Fluent redesign. (`3132ad9`)

## [1.25] - 2026-08-15

### Added
- Clear button (and Escape key) next to the toolbar search box. (`6623222`)
- Toolbar search box now also matches policies by registry key/value path,
  not just title/description/comment/ID. (`6623222`)

## [1.24] - 2026-08-15

### Added
- Tree pane, description pane, and State/Comment/ID column widths are now
  remembered across sessions, the same way window position/size already
  is. (`2551a58`)

## [1.23] - 2026-08-15

### Fixed
- Category tree pane had the same unbounded-growth issue just fixed for the
  description pane; now also pinned to a fixed width. (`b746f6c`)

## [1.22] - 2026-08-15

### Fixed
- Description pane (between the category tree and the policy list) grew to
  claim an ever-larger share of the window on wider screens despite only
  ever holding a couple of lines of text; now pinned to a fixed width so
  that space goes to the policy list instead. (`de70324`)

## [1.21] - 2026-08-15

### Fixed
- Policy list's ID column was pushed off-screen because the Name column's
  auto-fill logic didn't account for it, ballooning Name to consume the
  space ID needed (and wasting a lot of it on wide windows in the process);
  Name's growth is now capped. (`9a2339b`)

### Added
- Sorted column now shows a directional arrow in its header. (`9a2339b`)

## [1.20] - 2026-08-15

### Added
- Right-click a policy to copy its ID, name, or registry path to the
  clipboard. (`4b06216`)
- Policy list now has an ID column, and all columns are sortable by clicking
  their header. (`4b06216`)
- The toolbar search box now also matches partial policy IDs, not just
  title/description/comments. (`4b06216`)
- Policy details now show a breadcrumb-style template path (e.g. "Computer >
  Administrative Templates > ... > \<policy\>"). (`4b06216`)

## [1.19] - 2026-08-15

### Added
- Title bar and About dialog now show the app's own version number (e.g. "Policy Plus
  1.19"), not just the git commit hash. (`5ceb73e`)
- GitHub Release now also publishes a framework-dependent build
  (`PolicyPlus-framework-dependent.zip`) alongside the self-contained single-file EXE.
  (`5ceb73e`)

### Changed
- Version now bumps once per source-changing push, not per commit or per hand-picked batch
  — see the versioning note above. (`5ceb73e`)
- Release EXE renamed from `Policy Plus.exe` to `PolicyPlus.exe` (no space). (`5ceb73e`)

## [1.18] - 2026-08-15

### Added
- Self-contained single-file EXE (`PolicyPlus.exe`, no separate .NET runtime install
  needed) published as a rolling `latest` GitHub Release on every push. (`9b620e9`)

## [1.17] - 2026-08-15

### Fixed
- Selected-row highlight was invisible in Dark Mode whenever the tree/list wasn't focused;
  now an explicit theme-aware color instead of the OS default. (`7a37ba5`)
- Window resizing was sluggish with long lists, a regression from the highlight fix above;
  fixed with control double-buffering and a redundant resize handler removed. (`7a37ba5`)
- Search box had no visible border at rest in Dark Mode. (`d94977a`)
- Compiled EXE's file version/copyright were still the unbumped VB.NET-era defaults
  (`1.0.0.0`, 2016-2021 author); now synced from this file automatically on every build.
  (`79636f3`, `d4814c1`)
- Acquire ADMX Files pointed at a superseded Microsoft package (Sep 2025); updated to the
  current Oct 2025 (V2.0) release. (`3d74fc3`)

### Changed
- About dialog now credits this fork's maintainer alongside the original author, and links
  this fork's repo. (`279058c`)
- Internal performance/dedup cleanup in the [1.16] search and Favorites code (no
  user-visible change). (`ecc2c6d`)

## [1.16] - 2026-08-15

### Added
- File > Reset All to Default: resets every configured policy back to Not Configured, after
  confirmation. (`2ed5003`)
- File > Open REG File: imports a `.reg` file as a standalone editable source, independent
  of the currently-open source. (`1d11a7b`)
- Favorites: pin frequently-used policies to a dedicated node at the top of the category
  tree. (`86b4fad`)
- Always-visible search box in the main menu bar. (`84d7564`)

## [1.15] - 2026-08-14

### Added
- Prompt to Save/Discard/Cancel when closing with unsaved changes. (`bee465d`)
- Window size and position are now remembered across launches. (`c1ddb16`)
- Dark Mode support, with an Options > Color Mode menu. (`c48d762`)

### Fixed
- HiDPI displays no longer render blurry or wrongly sized. (`21ca6af`)
- "Supported on" field in Edit Policy Setting now scrolls instead of silently truncating.
  (`2f77f9e`)
- Description/policy-list divider is now a real draggable splitter. (`2f77f9e`)

## [1.14] - 2026-08-13

### Added
- [UPSTREAM_ISSUES.md](UPSTREAM_ISSUES.md): full triage of all 18 open upstream issues,
  with complexity estimates and a suggested approach for each. (`b261b1e`)

## [1.13] - 2026-08-13

### Fixed
- CI's dead "Upload to S3" step (targeted credentials this fork never had) replaced with a
  GitHub Actions artifact upload. (`cf5bdde`)

### Added
- CI now runs `dotnet test`. (`cf5bdde`)

### Changed
- `README.md` updated to match this fork (build badge, compile instructions, download
  link). (`cf5bdde`)

## [1.12] - 2026-08-13

### Removed
- Dead `PolicyPlus/PolicyPlusCs/` staging directory left over from the C# port. (`c1fe74f`)

### Changed
- `COMPILE.md`/`INSTALL.md`/`Docs/Components.md` updated to describe the current .NET
  10/C# build instead of the old VB.NET one. (`c1fe74f`)

## [1.11] - 2026-08-13

### Changed
- `PolFile`'s internal representation redesigned from a sentinel-marker scheme to an
  explicit key/value state tree — simpler and no longer scan-based. (`ae3a92e`)

### Fixed
- A real latent bug: `SetValue` could leave a stale deletion marker behind for value names
  starting with certain low-ASCII characters. (`ae3a92e`)

## [1.10] - 2026-08-13

### Changed
- Replaced all 57 uses of VB's `Interaction.MsgBox` with native WinForms
  `MessageBox.Show` (no behavior change). (`c55a98f`)

## [1.9] - 2026-08-13

### Fixed
- Category tree visibility was being recomputed redundantly on every filter change; now
  cached per tree walk. (`9c11554`)

## [1.8] - 2026-08-12

### Fixed
- 6 findings from a full code review (correctness fixes plus duplication cleanup,
  including extracting a shared `ResolveElementKey` helper). (`57e093a`, `dee364f`)

## [1.7] - 2026-08-12

### Fixed
- A real functional bug from the VB→C# port: VB treats `Nothing = ""` as true, C# doesn't —
  ~20 call sites across 6 files were silently broken (crashes and wrong registry keys used)
  until fixed. (`b035821`)

## [1.6] - 2026-08-12

### Changed
- VB.NET→C# port completed: all ~30 WinForms forms converted (via CodeConverter plus
  hand-fixes), full solution builds and runs. (`b035821`)

## [1.5] - 2026-08-12

### Changed
- Ported the core policy engine (`PolicyProcessing`) and `.reg`/`.pol`/`.spol`/`.cmtx` file
  handling to C#. (`b035821`)

## [1.4] - 2026-08-12

### Changed
- Ported the ADMX/ADML parsing core to C#. (`b035821`)

## [1.3] - 2026-08-12

### Changed
- Retargeted the VB.NET project to `net10.0-windows` (SDK-style project), motivated by
  #78/#73 needing modern WinForms APIs. (`401bb48`)

## [1.2] - 2026-08-12

### Changed
- Started the VB.NET → C# conversion (non-UI files first). (`b035821`)

## [1.1] - 2026-08-12

No code changes — established the two-stage modernization roadmap (retarget to
`net10.0-windows`, then convert to C#) and deferred upstream issue triage until after it
lands.

## [1.0] - 2026-08-12

### Added
- Forked from [Fleex255/PolicyPlus](https://github.com/Fleex255/PolicyPlus) at commit
  `a2a5379`. Baseline for tracking this fork's changes going forward.
