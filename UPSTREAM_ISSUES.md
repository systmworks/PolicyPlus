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

## How this fork relates to upstream

This fork underwent a full migration from VB.NET/.NET Framework 4.5.2 to C#/.NET 10
(see `CHANGELOG.md` for the full history), plus a from-scratch code review that fixed
10 findings and a redesign of the core `PolFile` class. None of the 18 issues below are
already fixed by that work, but two (#73, #78) had their *prerequisite* resolved by it —
see their entries.

## Summary table

| # | Title | Complexity | Overlaps / flags |
|---|---|---|---|
| [#10](https://github.com/Fleex255/PolicyPlus/issues/10) | Resizable divider between description/list panes | Low | — |
| [#104](https://github.com/Fleex255/PolicyPlus/issues/104) | "Supported on" field may overflow | Low | — |
| [#51](https://github.com/Fleex255/PolicyPlus/issues/51) | Prompt to save on close | Low | — |
| [#74](https://github.com/Fleex255/PolicyPlus/issues/74) | Reset all to default | Low-Medium | — |
| [#66](https://github.com/Fleex255/PolicyPlus/issues/66) | Edit imported REG file only | Low-Medium | — |
| [#56](https://github.com/Fleex255/PolicyPlus/issues/56) | Winget installer | Low-Medium | Blocked on a real tagged release existing |
| [#73](https://github.com/Fleex255/PolicyPlus/issues/73) | Dark Mode support | Medium | Foundation laid by the .NET 10 migration |
| [#78](https://github.com/Fleex255/PolicyPlus/issues/78) | Proper HiDPI support | Medium | Foundation laid by the .NET 10 migration |
| [#68](https://github.com/Fleex255/PolicyPlus/issues/68) | Favorites | Medium | — |
| [#47](https://github.com/Fleex255/PolicyPlus/issues/47) | Hotkey to change policy state | Medium | — |
| [#75](https://github.com/Fleex255/PolicyPlus/issues/75) | Policies missing under "User or Computer" | Investigated, no code bug found | Likely `DeduplicatePolicies` behavior, not a visibility defect — see detail entry |
| [#77](https://github.com/Fleex255/PolicyPlus/issues/77) | Export part of the policies | Medium-High | Overlaps #19 |
| [#19](https://github.com/Fleex255/PolicyPlus/issues/19) | Export/Import POL improvements | Medium-High | Overlaps #77 |
| [#17](https://github.com/Fleex255/PolicyPlus/issues/17) | Search UI improvement | Medium-High | Interacts with code touched for finding #5 |
| [#60](https://github.com/Fleex255/PolicyPlus/issues/60) | CLI navigation to policy by ID | Medium-High | Overlaps #46 |
| [#46](https://github.com/Fleex255/PolicyPlus/issues/46) | Command line options | High | Overlaps #60, needs scoping |
| [#72](https://github.com/Fleex255/PolicyPlus/issues/72) | Offline system editing | High | Largest single ask |
| [#20](https://github.com/Fleex255/PolicyPlus/issues/20) | `.pol` can't see Windows/Security settings | High / likely out of scope | Conflicts with README's stated priorities |

---

## Low complexity

### [#10 — Resizable divider between description and setting list](https://github.com/Fleex255/PolicyPlus/issues/10)

**Issue summary**: the divider between the policy description pane and the policy list is
fixed-width. The user wants a draggable splitter so the description area can be widened.

**Suggested fix**: replace the fixed-position controls in `Main.Designer.cs` with a
`SplitContainer` (or a `Splitter` control) between the two panes. This is a standard,
well-documented WinForms pattern — no novel design work needed, mostly mechanical
Designer-file surgery plus anchor/dock adjustments on the panes involved.

**Files**: `Main.cs`, `Main.Designer.cs`.

---

### [#104 — Edit Policy Setting dialog: "Supported on" field may overflow](https://github.com/Fleex255/PolicyPlus/issues/104)

**Issue summary**: the "Supported on" field in the Edit Policy Setting dialog is designed
to show a maximum of 3 lines of text, which isn't always enough (example given:
`Microsoft.Policies.WindowsUpdate:AutoUpdateCfg`). The text can technically be scrolled by
clicking in and pressing the down arrow, but there's no visible scrollbar, so this isn't
discoverable.

**Suggested fix**: either let the control auto-size to fit its content, or make sure a
scrollbar actually renders when the text overflows (this may already be a scrollable
control with the scrollbar just not showing — check the exact control type and its
`ScrollBars`/`AutoSize` properties in `EditSetting.cs`/`EditSetting.Designer.cs`).

**Files**: `EditSetting.cs`, `EditSetting.Designer.cs`.

---

### [#51 — Prompt to save or discard when closing the software](https://github.com/Fleex255/PolicyPlus/issues/51)

**Issue summary**: closing the app after making changes doesn't prompt to save — changes
are silently lost unless the user explicitly used *Save Policies* or Ctrl+S first. Wants a
save/discard/cancel prompt on close if there are unsaved changes.

**Suggested fix**: needs a "dirty" flag that gets set wherever policy state actually
changes (likely centered around wherever `PolicyProcessing.SetPolicyState`/`PolicySource`
mutations happen from the UI layer), checked in `Main`'s `FormClosing` event handler to
show a Save/Discard/Cancel prompt (via the now-native `MessageBox.Show`, post finding #9).
The tricky part isn't the prompt itself, it's finding every UI path that mutates policy
state and making sure the flag gets set consistently.

**Files**: `Main.cs` (primarily), wherever policy-state-mutating UI actions live.

---

## Low-Medium complexity

### [#74 — Reset all to default?](https://github.com/Fleex255/PolicyPlus/issues/74)

**Issue summary**: wants a bulk action to reset all currently-configured policies back to
Not Configured in one step, rather than doing it one at a time.

**Suggested fix**: iterate all policies with a non-`NotConfigured` state (via
`PolicyProcessing.GetPolicyState`) and call `SetPolicyState(..., PolicyState.NotConfigured, ...)`
on each, behind a confirmation dialog (this could affect a lot of policies at once — a
destructive bulk action, so the confirmation matters). Mostly reuses existing,
already-tested state-setting logic; the main new work is the iteration + UI entry point
(a menu item) + confirmation prompt.

**Files**: `Main.cs`, `PolicyProcessing.cs` (read-only reuse, no changes expected there).

---

### [#66 — Edit imported REG file only?](https://github.com/Fleex255/PolicyPlus/issues/66)

**Issue summary**: importing a REG file merges it with the live local-machine policies
instead of acting as a standalone editable source — the user wants to import, edit, and
export a REG file in isolation (this already works via POL files through *Open Policy
Resources*, just not REG).

**Suggested fix**: either (a) have REG import build a standalone in-memory `PolFile`-like
source instead of merging into the currently-open source, or (b) add REG as a first-class
selectable source type in `OpenPol.cs`'s *Open Policy Resources* dialog, alongside the
existing POL/Registry/user-hive options. `RegFile.cs` already implements enough of the
policy-source surface to make this plausible without a from-scratch rewrite (per
`Docs/Components.md`: "It implements just enough of `IPolicySource` to allow
`PolFile.Apply` to work on it, but it cannot be used as an actual policy source" — that
last part is exactly the gap this issue is asking to close).

**Files**: `RegFile.cs`, `OpenPol.cs`, `ImportReg.cs`.

---

### [#56 — Add an application installer so this can be made available to install using winget-cli](https://github.com/Fleex255/PolicyPlus/issues/56)

**Issue summary**: wants the app installable via `winget install`.

**Suggested fix**: write a winget package manifest (YAML) and submit it to the
`microsoft/winget-pkgs` repo. **Blocked on this fork actually having a tagged release to
point the manifest at** — right now, CI (`.github/workflows/latest.yml`) only produces a
per-commit downloadable Actions artifact, there's no Releases page. Cutting real releases
(with version tags) is a prerequisite, not part of this issue itself.

**Files**: none in this repo directly (a new manifest lives in the separate
`winget-pkgs` repo); prerequisite work would touch release/tagging process, possibly
`.github/workflows/latest.yml` or a new release workflow.

---

## Medium complexity

### [#73 — Dark Mode support](https://github.com/Fleex255/PolicyPlus/issues/73)

**Issue summary**: request for a dark mode option. Minimal issue body, but 3 comments
indicate ongoing interest.

**Foundation laid, not wired up**: this was one of two issues (with #78) that directly
motivated this fork's .NET Framework → .NET 10 migration — dark mode support didn't exist
in WinForms under .NET Framework at all. It does now.

**Suggested fix**: .NET 9 introduced `Application.SetColorMode(SystemColorMode mode)`
(`Classic` = light/legacy, `System` = follow the OS setting, `Dark` = force dark), carried
into .NET 10. Must be called once before `Application.Run()`. Still marked `[Experimental]`
(requires suppressing warning `WFO5001`), but control coverage matches this app's actual
usage well: `TreeView`, `ListView`, `MenuStrip`/`ToolStrip`, `ComboBox`, `DataGridView`,
`TextBox`, `Button` are all covered by the built-in dark renderer.

Recommended shape: wire the API call at startup, add a persisted `Light`/`Dark`/`System`
setting via the existing `ConfigurationStorage.cs` (same pattern already used for other
app settings), expose it as a menu toggle (e.g. under *View* or a new *Settings* menu).
Expect a manual QA pass across the ~30 forms afterward — experimental coverage can have
gaps, and custom-drawn elements (e.g. the category tree's folder icons) may need
dark-aware variants.

**Files**: `Program`/startup entry point (wherever `Application.Run` is called —
check `My Project\Application.myapp`/`MyApplication` startup path), `ConfigurationStorage.cs`,
`Main.cs` (menu wiring).

---

### [#78 — Proper support for HiDPI displays](https://github.com/Fleex255/PolicyPlus/issues/78)

**Issue summary**: on HiDPI displays, depending on Windows settings, the main window is
either too small or blurry.

**Foundation laid, not wired up**: the other migration-driving issue. Confirmed directly
this session: `PolicyPlus\My Project\app.manifest` has a `dpiAware`/`dpiAwareness` block,
but it's entirely commented out (lines 52-58), and `PolicyPlus.csproj` has no
`ApplicationHighDpiMode` setting either. So the app currently runs with no explicit DPI
awareness declaration at all — Windows falls back to bitmap-scaling it, which is exactly
the blurriness/wrong-sizing symptom reported.

**Suggested fix**: add `<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>` to
`PolicyPlus.csproj`. Modern SDK-style projects generate the correct manifest entries from
this property automatically — cleaner than hand-editing the legacy commented-out manifest
block. After enabling, needs a real test on a HiDPI or mixed-DPI multi-monitor setup to
catch any fixed-pixel-size layout assumptions left over in the older ported Designer files
(the VB→C# port was mechanical; Designer-generated fixed coordinates wouldn't have been
touched).

**Files**: `PolicyPlus.csproj` (one-line change), then whichever `*.Designer.cs` files
show layout problems during testing.

---

### [#68 — Feature Request: Favorites](https://github.com/Fleex255/PolicyPlus/issues/68)

**Issue summary**: wants a way to pin/favorite frequently-accessed policies (example given:
AppLocker exceptions) for quick access, rather than navigating the full tree each time.

**Suggested fix**: new UI element (a favorites panel, dropdown, or a dedicated pseudo-category
at the top of the tree) plus persistence of the favorited policy IDs. `ConfigurationStorage.cs`
already handles similar app-setting persistence (registry-backed) and is a reusable pattern
for storing a favorites list. Main design question: how favorites should behave across
different loaded ADMX workspaces (a favorited policy ID might not exist in every workspace).

**Files**: `Main.cs`, `ConfigurationStorage.cs`, likely a new small form or panel.

---

### [#47 — FR: Hotkey to fast change state of selected policy from main UI](https://github.com/Fleex255/PolicyPlus/issues/47)

**Issue summary**: wants keyboard shortcuts (suggests N/E/D, or 1/2/3) to set the selected
policy's state (Not Configured/Enabled/Disabled) directly from the main policy list,
without opening the full Edit Setting dialog. Suggests this could extend to multi-selection.

**Suggested fix**: a `KeyDown` handler on the policy `ListView` that calls
`PolicyProcessing.SetPolicyState` directly for simple policies. Complication: policies
with required additional elements (e.g. a list-type or text-type element) can't be
meaningfully "Enabled" via a single keystroke with no further input — needs a fallback
(e.g. open the full dialog for those, or skip/no-op the hotkey). Multi-select support
needs the same per-policy fallback logic applied across the selection.

**Files**: `Main.cs`.

---

### [#75 — Some options don't show when user "User or Computer" is selected?](https://github.com/Fleex255/PolicyPlus/issues/75)

**Issue summary**: the User-scope version of "Turn off handwriting personalization data
sharing" doesn't appear when the section filter is set to show both User and Computer
policies combined. Reporter suspects this may affect other policies too and is concerned
about silently missing policies as a result.

**Investigated — no code bug found.** `ShouldShowPolicy`/`PolicyVisibleInSection`
(`Main.cs`) were read line-by-line: the section-filter check is
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
This is `Main.cs`'s "Deduplicate Policies" feature, currently hidden from the View menu
(`Visible = false` at `Main.Designer.cs`) but still wired and reachable if re-enabled.

**Conclusion**: most likely explanation is that Deduplicate was run (or some other path
triggered it) against a workspace containing this pair, which is expected, working-as-designed
behavior for that feature, not a visibility bug. Could not reproduce a genuine bug in the
section-filter logic itself. Recommend closing as "investigated, likely explained" rather
than treating as an open defect, unless the reporter can confirm they never ran Deduplicate
and share the exact ADMX/policy where they saw this — that would point at a different,
still-unidentified cause.

**Files**: `Main.cs` (`ShouldShowPolicy`, `PolicyVisibleInSection` — read, not modified),
`PolicyProcessing.cs` (`DeduplicatePolicies` — the actual mechanism, also not modified).

---

## Medium-High complexity

### [#77 — Is there a way to export part of the policies?](https://github.com/Fleex255/PolicyPlus/issues/77)

**Issue summary**: wants to export only a subset of policies (e.g. just the "Network"
category) rather than the entire policy source, for troubleshooting purposes.

**Suggested fix**: new export UI allowing category/filter selection, plus export-logic
changes to only walk the selected subtree instead of the whole workspace. **Overlaps
#19**, which asks for essentially the same capability plus comment preservation — worth
designing these together rather than separately.

**Files**: `ExportReg.cs`, `Main.cs` (export menu wiring), possibly `RegFile.cs`/`SpolFile.cs`
depending on which export format(s) this is scoped to cover.

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

**Files**: `ExportReg.cs` (or wherever export logic lives), `EditPol.cs` (POL content
viewing), `README.md`/in-app help text for the procedural questions.

---

### [#17 — feature request: search ui improvement](https://github.com/Fleex255/PolicyPlus/issues/17)

**Issue summary**: wants an integrated search bar (top of the window, between the menu bar
and the main panes) instead of the current separate modal Find dialog, with live results
filtering the category tree as you type — comparable to the "Everything" search tool's UX.
Explicitly describes wanting search results to prune the tree down to just matching items
and their ancestor categories.

**Suggested fix**: a genuine UX rework, not a small tweak — replacing (or supplementing)
the modal `FindByText.cs`/`FindResults.cs` flow with an inline, always-visible search box
that live-filters as the user types. **Directly interacts with code modified this session**:
finding #5 added `ShouldShowCategoryCore`, a memoized category-visibility check scoped to
one `PopulateAdmxUi()` tree walk — a live-filter-as-you-type search is exactly the
workload that caching was built for (many tree repopulations in quick succession as the
user types), so that recent perf fix is directly relevant groundwork, not just adjacent.

**Files**: `Main.cs` (`PopulateAdmxUi`, `ShouldShowCategory`/`ShouldShowPolicy`),
`FindByText.cs` (reusable matching logic), likely new UI in `Main.Designer.cs`.

---

### [#60 — [Feature Request] Navigate to policies by ID with command line arguments](https://github.com/Fleex255/PolicyPlus/issues/60)

**Issue summary**: wants command-line arguments to select/edit a specific policy by its ID
in a running (or newly-launched) instance, for use by other tools that want to show users
"here's the actual setting" instead of just changing it silently. Proposes the app become
single-instance, with a second launch's arguments routed to the first instance via
cross-process communication.

**Suggested fix**: three pieces: (1) single-instance enforcement (named `Mutex` is the
standard WinForms approach); (2) cross-process argument routing to the already-running
instance (named pipe is the standard approach); (3) argument parsing + programmatic
navigation to a policy by ID — this last part **partly exists already** via `FindById.cs`,
which already implements "jump to a policy object by its unique ID" as a user-facing
dialog; the new work is triggering that same navigation programmatically from parsed CLI
args instead of a dialog. **Overlaps #46** (broader/vaguer CLI request) — this issue is
the more concretely-specified one of the two.

**Files**: new: single-instance/IPC plumbing (likely in the startup path, `Program`/
`MyApplication`). Existing: `FindById.cs` (navigation logic to reuse).

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
as the home for native interop declarations), parse the offline `PolicyDefinitions` folder
from the mounted drive instead of the live system's, and thread an offline key-path prefix
through the existing `RegistryPolicyProxy`/`IPolicySource` machinery. This is the largest
single ask across all 18 issues — real new subsystem work (hive mounting/unmounting
lifecycle, error handling for locked/in-use hives, user-profile discovery), not an
extension of existing code paths. The author themselves calls it "a sizeable task."
The documentation-clarity note (save/apply confusion) is worth splitting off as its own
much smaller, independent fix regardless of whether the main feature gets picked up.

**Files**: `PInvoke.cs` (new native declarations), `OpenAdmxFolder.cs`/`OpenPol.cs` (new UI
entry point), `PolicySource.cs`/`RegistryPolicyProxy` (offline-prefixed operations), plus
likely a new dedicated form for offline-system selection.

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
