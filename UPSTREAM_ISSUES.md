# Upstream issues triage

Tracking the 18 open issues on the upstream repo this fork was created from,
[Fleex255/PolicyPlus](https://github.com/Fleex255/PolicyPlus/issues), triaged for
work on this fork ([systmworks/PolicyPlus](https://github.com/systmworks/PolicyPlus)).

This file is meant to be self-contained enough to hand any single issue off to an agent
or contributor without them needing prior conversation context — each entry has the
issue's own summary, a complexity estimate, and a suggested starting approach.

**The "suggested fix" in each entry is a starting hypothesis, not a locked-in design** —
every issue should be investigated properly (re-read the live issue thread for any new
comments, confirm the suggested approach still fits the current code) before implementing.
For quick status, see `CHANGELOG.md`; this file carries the full detail.

**Re-triaged 2026-08-16** against the app's current state (post `[1.26]` WPF-UI migration).
File references below use current `Views/*.xaml.cs` paths, not the WinForms-era filenames
(`Main.cs`, `EditSetting.Designer.cs`, etc.) this doc originally used before the migration.

## How this fork relates to upstream

This fork underwent a full migration from VB.NET/.NET Framework 4.5.2 to C#/.NET 10, then a
second migration from WinForms to WPF-UI (Fluent design) (see `CHANGELOG.md` for the full
history), plus two from-scratch code reviews. A substantial amount of feature work landed on
top of both migrations since this file was first written — **8 of the original 18 issues are
now resolved** (see below) and weren't previously marked as such.

## Resolved since this file was first written

None of these were closed out in this doc at the time — cross-checked against `CHANGELOG.md`
and current source this session:

| # | Title | Resolved by |
|---|---|---|
| [#10](https://github.com/Fleex255/PolicyPlus/issues/10) | Resizable divider between description/list panes | `[1.15]` "Description/policy-list divider is now a real draggable splitter" (`2f77f9e`) — confirmed live: `Views/MainWindow.xaml` uses `GridSplitter` (2 instances). |
| [#104](https://github.com/Fleex255/PolicyPlus/issues/104) | "Supported on" field may overflow | `[1.15]` "'Supported on' field in Edit Policy Setting now scrolls instead of silently truncating" (`2f77f9e`). |
| [#51](https://github.com/Fleex255/PolicyPlus/issues/51) | Prompt to save on close | `[1.15]` "Prompt to Save/Discard/Cancel when closing with unsaved changes" (`bee465d`) — confirmed live: `Views/MainWindow.xaml.cs:1488` `Window_Closing`, line 1494 prompts via `MsgBoxCompat.Show(...YesNoCancel...)`. |
| [#74](https://github.com/Fleex255/PolicyPlus/issues/74) | Reset all to default | `[1.16]` "File > Reset All to Default: resets every configured policy back to Not Configured, after confirmation" (`2ed5003`) — confirmed live: `Views/MainWindow.xaml.cs:1168` `ResetAllToDefaultMenuItem_Click`. |
| [#66](https://github.com/Fleex255/PolicyPlus/issues/66) | Edit imported REG file only | `[1.16]` "File > Open REG File: imports a .reg file as a standalone editable source, independent of the currently-open source" (`1d11a7b`) — confirmed live: `Views/MainWindow.xaml:251` `OpenRegFileMenuItem`. |
| [#68](https://github.com/Fleex255/PolicyPlus/issues/68) | Favorites | `[1.16]` "Favorites: pin frequently-used policies to a dedicated node at the top of the category tree" (`86b4fad`) — confirmed live: `Views/MainWindow.xaml.cs:84,189,235,787,809`. |
| [#73](https://github.com/Fleex255/PolicyPlus/issues/73) | Dark Mode support | `[1.15]` "Dark Mode support, with an Options > Color Mode menu" (`c48d762`), implemented via `ThemeService.cs` (wraps `Wpf.Ui.Appearance.ApplicationThemeManager`) rather than this doc's original suggested `.NET`-experimental-API approach — WPF-UI shipped a first-class solution once the app migrated off WinForms. |
| [#78](https://github.com/Fleex255/PolicyPlus/issues/78) | Proper HiDPI support | `[1.15]` "HiDPI displays no longer render blurry or wrongly sized" (`21ca6af`). **Correcting this doc's own prior error**: an earlier version of this entry claimed `app.manifest`'s `dpiAware`/`dpiAwareness` block was "entirely commented out" — verified directly this session, it is **not**; it's live and correctly configured (`dpiAware=true/PM`, `dpiAwareness=PerMonitorV2`), with a comment explaining `ApplicationHighDpiMode` is deliberately not used because the custom manifest already covers it. |

**Partially addressed** — worth a closer look rather than fully closed:

- [#17](https://github.com/Fleex255/PolicyPlus/issues/17) — see its entry below. An always-visible toolbar search box now exists (`[1.16]`/`[1.20]`/`[1.25]`), replacing the old modal-only Find flow for the common case — but it filters on **Enter**/button click (`Views/MainWindow.xaml.cs:917-944`, `SearchTextbox_KeyDown`/`RunSearch`), not live as-you-type as the issue specifically asked for.

## Summary table — still open

| # | Title | Complexity | Overlaps / flags |
|---|---|---|---|
| [#56](https://github.com/Fleex255/PolicyPlus/issues/56) | Winget installer | Low-Medium | Blocker resolved — see entry |
| [#47](https://github.com/Fleex255/PolicyPlus/issues/47) | Hotkey to change policy state | Considered, declined | Key-scheme problem — see detail entry; decision predates WPF migration, worth reverifying |
| [#75](https://github.com/Fleex255/PolicyPlus/issues/75) | Policies missing under "User or Computer" | Investigated, no code bug found | Likely `DeduplicatePolicies` behavior, not a visibility defect — see detail entry |
| [#77](https://github.com/Fleex255/PolicyPlus/issues/77) | Export part of the policies | Medium-High | Overlaps #19 |
| [#19](https://github.com/Fleex255/PolicyPlus/issues/19) | Export/Import POL improvements | Medium-High | Overlaps #77 |
| [#17](https://github.com/Fleex255/PolicyPlus/issues/17) | Search UI improvement | Medium-High | Partially addressed already — see above |
| [#60](https://github.com/Fleex255/PolicyPlus/issues/60) | CLI navigation to policy by ID | Medium-High | Overlaps #46 |
| [#46](https://github.com/Fleex255/PolicyPlus/issues/46) | Command line options | High | Overlaps #60, needs scoping |
| [#72](https://github.com/Fleex255/PolicyPlus/issues/72) | Offline system editing | High | Largest single ask |
| [#20](https://github.com/Fleex255/PolicyPlus/issues/20) | `.pol` can't see Windows/Security settings | High / likely out of scope | Conflicts with README's stated priorities |

---

## Low-Medium complexity

### [#56 — Add an application installer so this can be made available to install using winget-cli](https://github.com/Fleex255/PolicyPlus/issues/56)

**Issue summary**: wants the app installable via `winget install`.

**Suggested fix**: write a winget package manifest (YAML) and submit it to the
`microsoft/winget-pkgs` repo. **Blocker resolved**: this doc originally noted the fork had
no tagged releases, only per-commit CI artifacts. That's no longer true — releases are now
tagged (`v<X.Y>`, e.g. `v1.19`+) via `.github/workflows/latest.yml`'s `softprops/action-gh-release`
step, with both a self-contained `PolicyPlus.exe` and a framework-dependent zip published per
release (`[1.18]`/`[1.19]`). This issue is now actually actionable — the only remaining work
is writing and submitting the manifest itself.

**Files**: none in this repo directly (a new manifest lives in the separate
`winget-pkgs` repo).

---

## Medium complexity

### [#47 — FR: Hotkey to fast change state of selected policy from main UI](https://github.com/Fleex255/PolicyPlus/issues/47)

**Issue summary**: wants keyboard shortcuts (suggests N/E/D, or 1/2/3) to set the selected
policy's state (Not Configured/Enabled/Disabled) directly from the main policy list,
without opening the full Edit Setting dialog. Suggests this could extend to multi-selection.

**Suggested fix**: a `KeyDown` handler on the policy list (`Views/MainWindow.xaml.cs`,
`PoliciesList`) that calls `PolicyProcessing.SetPolicyState` directly for simple policies.
Complication: policies with required additional elements (e.g. a list-type or text-type
element) can't be meaningfully "Enabled" via a single keystroke with no further input —
needs a fallback (e.g. open the full dialog for those, or skip/no-op the hotkey).
Multi-select support needs the same per-policy fallback logic applied across the selection.

**Considered and declined** (decision made pre-WPF-migration, against the old WinForms
`ListView`). Fully scoped at the time, verified directly against
`PolicyProcessing.SetPolicyState`'s actual behavior: Not Configured (`ForgetPolicy`) and
Disabled are always safe to apply directly with no element values; Enabled is too, but only
when the policy has no elements — with elements, it falls back to opening the full Edit
Setting dialog, same as the issue's own suggested fix. The blocker was the key scheme: the
mnemonic option (N/E/D) would silently override WinForms `ListView`'s built-in
type-ahead-to-select behavior for any policy name starting with those letters, so the only
collision-free option was digits (1/2/3) — judged too unintuitive for what the feature is
worth, so it wasn't implemented. **Worth reverifying**: `PoliciesList` is now a WPF control
post-migration, which may have different (or no) built-in type-ahead-to-select behavior —
the original collision concern may no longer apply, which would reopen the N/E/D option.
Revisit if a better key scheme comes up either way (e.g. a modifier-based combo like
Ctrl+Alt+letter, which wouldn't collide with type-ahead regardless).

**Files**: `Views/MainWindow.xaml.cs`.

---

### [#75 — Some options don't show when user "User or Computer" is selected?](https://github.com/Fleex255/PolicyPlus/issues/75)

**Issue summary**: the User-scope version of "Turn off handwriting personalization data
sharing" doesn't appear when the section filter is set to show both User and Computer
policies combined. Reporter suspects this may affect other policies too and is concerned
about silently missing policies as a result.

**Investigated — no code bug found.** `ShouldShowPolicy`/`PolicyVisibleInSection`
(`Views/MainWindow.xaml.cs`) were read line-by-line: the section-filter check is
`(int)(Policy.RawPolicy.Section & Section) > 0`, which is correct bitwise-flag logic for
`AdmxPolicySection`'s `Machine=1, User=2, Both=3` — a User-only (`Section=2`) policy is
not excluded when the combined view (`Section&3`) is active. `PopulateAdmxUi`'s list
population also doesn't de-duplicate entries by `DisplayName`, so two distinctly-ID'd
policies sharing a display name would normally both show as separate rows.

The one mechanism in this codebase that *does* make one twin of an identical-name
Machine/User policy pair disappear is `PolicyProcessing.DeduplicatePolicies`
(`PolicyProcessing.cs:177-197`): for a pair with the same category, display name,
explanation text, and registry key whose sections sum to `Both`, it permanently removes
one twin (`Workspace.Policies.Remove(a.UniqueID)`) and relabels the survivor as
`Section = Both`. A real matching ADMX example was found in the shipped
`PolicyDefinitions\Globalization.admx`: `ImplicitDataCollectionOff_1` (`class="User"`) and
`ImplicitDataCollectionOff_2` (`class="Machine"`) — two separate policy objects with
identical display name, explanation, and registry key (`SOFTWARE\Policies\Microsoft\InputPersonalization`)
— the historical "Turn off handwriting personalization data sharing" pair (renamed to
"Turn off automatic learning" in current ADML strings), exactly the issue's own example.
This is the app's "Deduplicate Policies" feature — **its current menu visibility/entry point
should be reverified against `Views/MainWindow.xaml`** (this doc previously cited a WinForms
`Main.Designer.cs` `Visible = false` setting for it, which no longer exists in that form
post-migration; confirm whether/how it's currently exposed before treating this conclusion
as final).

**Conclusion**: most likely explanation is that Deduplicate was run (or some other path
triggered it) against a workspace containing this pair, which is expected, working-as-designed
behavior for that feature, not a visibility bug. Could not reproduce a genuine bug in the
section-filter logic itself. Recommend closing as "investigated, likely explained" rather
than treating as an open defect, unless the reporter can confirm they never ran Deduplicate
and share the exact ADMX/policy where they saw this — that would point at a different,
still-unidentified cause.

**Files**: `Views/MainWindow.xaml.cs` (`ShouldShowPolicy`, `PolicyVisibleInSection`, "Deduplicate
Policies" menu wiring — read, not modified), `PolicyProcessing.cs` (`DeduplicatePolicies` — the
actual mechanism, also not modified).

---

## Medium-High complexity

### [#77 — Is there a way to export part of the policies?](https://github.com/Fleex255/PolicyPlus/issues/77)

**Issue summary**: wants to export only a subset of policies (e.g. just the "Network"
category) rather than the entire policy source, for troubleshooting purposes.

**Suggested fix**: new export UI allowing category/filter selection, plus export-logic
changes to only walk the selected subtree instead of the whole workspace. **Overlaps
#19**, which asks for essentially the same capability plus comment preservation — worth
designing these together rather than separately.

**Files**: `Views/ExportRegWindow.xaml.cs`, `Views/MainWindow.xaml.cs` (export menu wiring),
possibly `RegFile.cs`/`SpolFile.cs` depending on which export format(s) this is scoped to
cover.

---

### [#19 — Export/Import POL improvements and issues](https://github.com/Fleex255/PolicyPlus/issues/19)

**Issue summary**: a bundle of related asks and questions: (1) exporting with a "commented
only" filter active doesn't preserve comments in the exported POL, and doesn't actually
filter to just the commented/configured subset either — asks for a checkbox-based
selective export with comments included; (2) wants a better way to view POL file contents
than the raw editor, ideally filtered-tree-based; (3) confusion about what the "ADMX
workspace" is for, since it stays empty even after opening a Local GPO or POL file; (4)
confusion about the correct procedure to restore/apply a saved POL file.

**Suggested fix**: the filtered-export-with-comments part (1) **overlaps #77** — same
underlying capability. Part (2) is a smaller, separate UI idea (a filtered tree view of
POL contents, as an alternative to the raw key/value editor). Parts (3) and (4) are
**not code bugs** — they're user confusion about existing concepts (the ADMX workspace is
the loaded policy *definitions*, separate from the policy *state* sources being edited;
save/apply procedure is already documented behavior, just non-obvious). These would be
better addressed by clarifying the README/in-app UI copy than by a code change.

**Files**: `Views/ExportRegWindow.xaml.cs` (or wherever export logic lives),
`Views/EditPolWindow.xaml.cs` (POL content viewing), `README.md`/in-app help text for the
procedural questions.

---

### [#17 — feature request: search ui improvement](https://github.com/Fleex255/PolicyPlus/issues/17)

**Issue summary**: wants an integrated search bar (top of the window, between the menu bar
and the main panes) instead of the current separate modal Find dialog, with live results
filtering the category tree as you type — comparable to the "Everything" search tool's UX.
Explicitly describes wanting search results to prune the tree down to just matching items
and their ancestor categories.

**Partially addressed already** (see "Resolved" section above for the full picture): the app
now has exactly the "integrated search bar, not a modal dialog" part of this request —
`Views/MainWindow.xaml.cs:917-944` (`SearchTextbox_KeyDown`/`RunSearch`) — an always-visible
toolbar search box that drives `MoveToVisibleCategoryAndReload()` to filter the tree/list via
`PolicySearch.BuildMatcher`. **What's still missing**: it requires pressing Enter or clicking
the search button — it does not live-filter as the user types character-by-character, which
is the specific UX the issue asks for (comparing it to "Everything").

**Suggested fix for the remaining gap**: wire a `TextChanged` handler on `SearchTextbox` (debounced,
since re-running `MoveToVisibleCategoryAndReload()` on every keystroke against a large ADMX
workspace could be expensive) instead of/alongside the current Enter-only trigger.

**Files**: `Views/MainWindow.xaml.cs` (`SearchTextbox_KeyDown`/`RunSearch`/
`MoveToVisibleCategoryAndReload`, `PopulateAdmxUi`/`ShouldShowCategory`/`ShouldShowPolicy`),
`PolicySearch.cs` (matching logic, already reusable).

---

### [#60 — [Feature Request] Navigate to policies by ID with command line arguments](https://github.com/Fleex255/PolicyPlus/issues/60)

**Issue summary**: wants command-line arguments to select/edit a specific policy by its ID
in a running (or newly-launched) instance, for use by other tools that want to show users
"here's the actual setting" instead of just changing it silently. Proposes the app become
single-instance, with a second launch's arguments routed to the first instance via
cross-process communication.

**Suggested fix**: three pieces: (1) single-instance enforcement (named `Mutex` is the
standard approach, works the same under WPF as WinForms); (2) cross-process argument routing
to the already-running instance (named pipe is the standard approach); (3) argument parsing +
programmatic navigation to a policy by ID — this last part **partly exists already** via
`Views/FindByIdWindow.xaml.cs`, which already implements "jump to a policy object by its
unique ID" as a user-facing dialog; the new work is triggering that same navigation
programmatically from parsed CLI args instead of a dialog. **Overlaps #46** (broader/vaguer
CLI request) — this issue is the more concretely-specified one of the two.

**Files**: new: single-instance/IPC plumbing (likely in `App.cs`'s startup path). Existing:
`Views/FindByIdWindow.xaml.cs` (navigation logic to reuse).

---

## High complexity / needs scoping

### [#46 — Command Line options](https://github.com/Fleex255/PolicyPlus/issues/46)

**Issue summary**: general request for command-line functionality, citing `LGPO.exe` as
an annoying alternative. References an abandoned third-party fork attempt
([daveMueller/PolicyPlus](https://github.com/daveMueller/PolicyPlus/commits/UseCommandLineUtils))
that apparently required splitting into separate GUI/console builds and was never
finished or upstreamed.

**Suggested fix**: **overlaps #60 heavily**, but has no concrete spec of its own beyond
"CLI features would be good." Needs real scoping — which specific operations should be
CLI-drivable (get a policy's state? set it? import/export? all of the above?) — before
this is implementable as a single piece of work. Recommend treating #60 as the concrete
first slice of this (navigate-to-policy-by-ID) and revisiting the rest of #46's scope
once that lands and there's a clearer pattern for how CLI operations plug into the app.

**Files**: TBD, pending scoping.

---

### [#72 — Add ability to specify Windows folder for offline systems](https://github.com/Fleex255/PolicyPlus/issues/72)

**Issue summary**: wants to point Policy Plus at an offline Windows installation (e.g. a
different drive plugged in over USB, or a dual-boot partition) and edit its Group Policy
settings without booting into it — similar to how Sysinternals Autoruns can operate on
offline installs. The issue author sketches out the mechanism themselves: locate and mount
the offline SOFTWARE/SYSTEM registry hives (e.g. as `HKLM\OFFLINE_X_SOFTWARE`), find and
mount user profile hives (`ntuser.dat`) similarly, then read the offline system's own
ADMX files and prefix all registry operations with the offline-mounted key paths instead
of the live `HKLM`/`HKCU`. Also separately notes that the requirement to explicitly
"Save to Registry" and "Apply Policy" isn't obvious to new users and causes confusion
("I had thought that all the edits were either lost or done in vain") — this documentation
point could be addressed independently and much more cheaply than the main feature.

**Suggested fix**: the author's own sketch is a reasonable starting design: use
`RegLoadAppKey`/`RegLoadKey`-style hive mounting (P/Invoke — `PInvoke.cs` already exists
as the home for native interop declarations, and `Privilege.cs`'s `EnablePrivilege` already
covers acquiring the backup/restore privileges hive-loading needs), parse the offline
`PolicyDefinitions` folder from the mounted drive instead of the live system's, and thread an
offline key-path prefix through the existing `RegistryPolicyProxy`/`IPolicySource` machinery.
This is the largest single ask across all 18 issues — real new subsystem work (hive
mounting/unmounting lifecycle, error handling for locked/in-use hives, user-profile
discovery), not an extension of existing code paths. The author themselves calls it "a
sizeable task." The documentation-clarity note (save/apply confusion) is worth splitting off
as its own much smaller, independent fix regardless of whether the main feature gets picked up.

**Files**: `PInvoke.cs` (new native declarations), `Views/OpenAdmxFolderWindow.xaml.cs`/
`Views/OpenPolWindow.xaml.cs` (new UI entry point), `PolicySource.cs`/`RegistryPolicyProxy`
(offline-prefixed operations), plus likely a new dedicated window for offline-system selection.

---

### [#20 — .pol can not see windows settings/security settings](https://github.com/Fleex255/PolicyPlus/issues/20)

**Issue summary**: opening a `.pol` file works, but doesn't show what's configured under
Group Policy's "Windows Settings" or "Security Settings" branches.

**Likely out of scope as currently defined**: Windows Settings and Security Settings are
different Group Policy client-side extensions entirely — they use the SecEdit `.inf`
format (and other extension-specific formats), not the ADMX/`registry.pol` machinery this
entire application is built around. This isn't a bug in the existing ADMX/POL handling;
it's a request to support a fundamentally different policy category with its own parser,
editor UI, and file format, unrelated to everything else in this codebase.

This project's own `README.md` already states: *"Non-Registry-based policies (i.e. items
outside the Administrative Templates branch of the Group Policy Editor) currently have no
priority, but they may be reconsidered at a later date."* Recommend triaging this as
won't-fix-for-now and saying so on the issue, rather than scoping it for implementation,
unless the project's priorities change.

**Files**: N/A unless priorities change — would require an entirely new subsystem.
