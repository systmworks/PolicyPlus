# Changelog

All notable changes to this fork are documented here. Format is loosely based on
[Keep a Changelog](https://keepachangelog.com/), version numbers are custom to this fork
(see note below).

## Versioning note

Upstream ([Fleex255/PolicyPlus](https://github.com/Fleex255/PolicyPlus)) does not state a
meaningful semantic version. `AssemblyVersion`/`AssemblyFileVersion` are hardcoded to
`1.0.0.0` and never bumped; the version actually shown at runtime comes from
`version.bat` embedding `git describe --always` into `Version.vb` at build time — i.e.
upstream tracks itself by commit, not by release number. For this fork, upstream's state
at fork time is treated as **1.0**, and each notable batch of work increments by **0.1**.

## [1.7] - Fixed a real runtime bug: VB's `Nothing = ""` vs C#'s `null == ""`

**First real functional smoke test of the C# build** (1.6 was build-clean but never actually
run): launching `Policy Plus.exe` threw `Microsoft.VisualBasic.Core: Policy definitions
could not be loaded: Object reference not set to an instance of an object.` on startup,
then opened with an empty policy list — a genuine regression versus the working VB.NET
build (1.3), not a pre-existing bug or environment issue.

**Root cause**: VB.NET's `=`/`<>` string operators treat `Nothing` as equal to `""` (e.g.
`Nothing = ""` is `True`). Every hand-ported file that translated a VB `X = ""` /
`X <> ""` check literally to C#'s `X == ""` / `X != ""` is wrong whenever `X` can actually
be `null` — and it very often can be, because `AttributeOrNull(...)` (used throughout
`AdmxFile.cs`/`AdmlFile.cs` for optional XML attributes like a category's `parentCategory`
ref, a policy's top-level `valueName`, an element's `key` override) returns `null`, not
`""`, when the attribute is absent — which is the *common* case, not an edge case (e.g.
every ADMX file's top-level categories have no `parentCategory`, and most policy elements
don't override the inherited registry key).

Concretely, `AdmxBundle.cs`'s `if (cat.RawCategory.ParentID != "")` let a `null` `ParentID`
(any top-level category) through the guard and crashed on `ResolveRef`'s
`Ref.Contains(":")` — the actual NRE the popup reported. A second, more consequential
class of the same bug was silently wrong rather than crashing: throughout
`PolicyProcessing.cs`, patterns like `elem.RegistryKey == "" ? rawpol.RegistryKey :
elem.RegistryKey` failed to fall back to the policy's key when `elem.RegistryKey` was
`null` (true for most elements), so the wrong (`null`) registry key would silently get
used for reading/writing policy values — a correctness bug, not just a crash, and the
kind that's easy to miss because it doesn't necessarily throw.

**Fix**: audited every `== ""`/`!= ""` string comparison across every hand-ported file
(`AdmxBundle.cs`, `PolicyProcessing.cs`, `PolicySource.cs`, `PolicyLoader.cs`,
`RegFile.cs`, `SpolFile.cs`) and replaced the ones operating on a field/property that can
be `null` with `string.IsNullOrEmpty(x)` / `!string.IsNullOrEmpty(x)`. Left alone the ones
operating on values that are structurally guaranteed non-null (`.Split(...)`/`.Trim()`
results, method parameters always called with a literal `""`) — those were already
correct. ~20 call sites fixed across 6 files, most of them in `PolicyProcessing.cs`.

Note for reference: the codeconv-generated forms (1.6) already handled this correctly in
the one place it came up (`Main.cs`: `(admxSource ?? "") != (defaultAdmxSource ?? "")`) —
the Roslyn-based tool detected the VB semantic and compensated. Hand-porting missed it in
several places; this is the concrete cost of manual translation the changelog flagged
as a risk back in the Stage 2 plan.

**Verified**: relaunched successfully — category tree and policy list populate correctly,
and the Edit Policy Setting dialog opens and renders a real policy (description, radio
buttons, "Supported on" info) correctly. Stage 2's C# port is now functionally confirmed,
not just build-clean.

## [1.6] - Stage 2 complete: full C# build, 0 errors — WinForms via CodeConverter

**`PolicyPlusCs` now builds as a real WinExe** (`dotnet build`, no `-p:OutputType=Library`
override needed): `Policy Plus.exe` is produced. Every VB.NET file has a C# counterpart.

Got here via [icsharpcode/CodeConverter](https://github.com/icsharpcode/CodeConverter)
rather than hand-porting the remaining ~30 forms:
- Set up the tool: `dotnet nuget add source https://api.nuget.org/v3/index.json` (this
  environment had zero NuGet sources configured — nothing to do with the tool itself),
  then `dotnet tool install ICSharpCode.CodeConverter.codeconv --global`. **No Visual
  Studio install was needed** despite some docs implying otherwise — the CLI tool works
  standalone against the .NET SDK.
- Ran `codeconv -f -o <scratch dir> PolicyPlus/PolicyPlus.sln` (output to a throwaway
  directory, not in place) as a test first. All 140 files converted without the tool
  itself erroring; the resulting solution had **33 build errors across 10 of 140 files**.
- Compared those 10 files against work already done here: 4 of them
  (`Privilege.vb`, `PolicyLoader.vb`, `PolicyProcessing.vb`, `AdmxFile.vb`) were files
  already hand-ported in 1.2–1.5 with **zero** errors — the tool tripped on exactly the
  patterns called out below, which manual translation had already caught. That comparison
  is the reason the remaining ~30 forms were adopted from the tool's output rather than
  hand-ported from scratch: the tool gets the mechanical 95% (especially every single
  `.Designer.cs` file, which converted with zero errors) right, and the failure modes are
  narrow and predictable enough to fix confidently.
- Copied the tool's output for every file not already hand-ported — all form
  `.cs`/`.Designer.cs`/`.resx` triplets, `Resources.Designer.cs`/`.resx`, and the `My
  Project/` support files (`AssemblyInfo.cs`, `Application.Designer.cs`/`.myapp`,
  `Settings.Designer.cs`/`.settings`, `app.manifest`, and four
  `MyNamespace.*.Designer.cs` files — the tool's C# equivalent of VB's `My.*` namespace
  magic, e.g. `My.MyProject.Forms.DetailPolicy.PresentDialog(...)` for VB's default-form-instance
  feature) — into `PolicyPlusCs/`, and rebuilt `PolicyPlusCs.csproj` to mirror the curated
  item-list style established in 1.2 (the tool's own generated `.csproj` was usable almost
  verbatim, since it was generated by converting the already-modernized VB project from
  Stage 1).
- Fixed the 9 real errors that carried over into the adopted files (2 more error sites
  than the original 7-in-untouched-files count, since `DetailAdmx.cs`'s 16 errors were all
  one root cause counted per call site):
  - **7× "use of unassigned local variable"** (`EditPol.cs` ×2, `Main.cs` ×2,
    `FilterOptions.cs` ×2, `InspectPolicyElements.cs` ×1) — all the same VB
    recursive-lambda-via-pre-declared-variable pattern from 1.4/1.5
    (`Dim x As Action(Of T) : x = Sub(...) ... x(...) ... End Sub`). CodeConverter
    translates this literally (`Action<T> x; x = new Action<T>(...)`), which C#'s
    definite-assignment analysis rejects even though it's safe at runtime. Fixed the same
    way as every prior occurrence: converted to a C# local function, which supports
    recursion natively.
  - **1× `Func<object,string>` delegate mismatch** (`DetailAdmx.cs`, 16 error sites from
    one cause) — VB's relaxed delegate conversion let a `Function(p As PolicyPlusPolicy) ...`
    literal satisfy a parameter declared `Func(Of Object, String)`; C# lambdas must match
    the delegate's parameter type exactly. Fixed by making the local function generic
    (`void fillListview<T>(...)`) instead of loosely typed as `object`, so each call site's
    concrete type flows through via inference — more correct than the original's `object`
    typing, not just a workaround.
  - **2× "Delegate 'EventHandler' does not take 0 arguments"** (`EditSetting.cs`) — VB
    permits `AddHandler control.Event, Sub() ...` with fewer parameters than the event
    delegate signature; C# requires the full `(sender, e)` signature on every handler.
    Added the two unused parameters.
- **Not done yet**: launching the C# build and clicking through it (same caveat as every
  prior entry — build-clean is not run-correct), and swapping `PolicyPlusCs` in as the
  active project in the solution.

## [1.5] - Stage 2: all core business logic ported — 19 files, 0 errors

Ported the remaining core-logic files: `PolicyProcessing.vb` → `PolicyProcessing.cs` (28.9 KB,
the policy engine — read/write/detect policy state), `RegFile.vb` → `RegFile.cs` (.reg
import/export), `SpolFile.vb` → `SpolFile.cs` (the internal "Semantic Policy" text
format), `CmtxFile.vb` → `CmtxFile.cs` (comment sidecar files). **Every non-UI file is now
in C# and the project builds clean as a library** (`dotnet build -p:OutputType=Library`:
0 warnings, 0 errors). Only the ~30 WinForms forms + their `.Designer.vb`/`.resx` pairs
remain — a different kind of task (mechanical UI declarations rather than logic), paused
here to check in before starting it.

Notable translation decisions in this batch:
- `PolicyProcessing.vb` used VB lambdas with `ByRef Decimal` accumulator parameters
  (`checkOneVal`, `checkValList` in `GetPolicyState`) — C# `Func`/`Action` delegates can't
  have `ref` parameters, so these became C# **local functions** with `ref decimal`
  parameters instead, which do support `ref` natively and required no other change to the
  call sites' logic.
- Every `Dim x As SomeSubclass = elem` pattern (implicit downcast from `PolicyElement` to
  `BooleanPolicyElement`/`ListPolicyElement`/etc., or from `AdmxPolicySection` enum
  arithmetic) became an explicit C# cast — these were silent runtime checks under
  `Option Strict Off` with no C# equivalent shorthand.
  `AdmxPolicySection` enum arithmetic (`a.Section + b.Section` in `DeduplicatePolicies`)
  needed explicit `(int)` casts since C# doesn't overload `+` for enums the way VB does.
- `RegistryKeyValuePair` implemented `IEquatable(Of T)` via a VB `Implements` clause that
  renamed the interface method to `EqualsRKVP` — nothing outside this file called it by
  that name, so the C# port simplifies to the idiomatic `bool Equals(RegistryKeyValuePair
  other)` directly; behavior is unchanged for every actual caller (`List.Contains`, which
  uses `IEquatable<T>` either way).
- `RegFile.vb`'s hex-dump `.reg` writer relies on `Convert.ToString(value, 16)` overloads
  that don't exist for `uint`/enum/`byte` directly in either language — VB was silently
  widening through `Int64`/`Int32` to find a matching overload. Made that widening
  explicit: `Convert.ToString((long)Convert.ToUInt32(...), 16)` for DWORDs,
  `Convert.ToString((int)value.Kind, 16)` for the registry type code.
- `SpolFile.vb`'s `GetFragment` serializer switched on `kv.Value.GetType()` compared
  against `GetType(Integer)`/`GetType(UInteger)`/etc. — C#'s `switch` doesn't support
  `Type` values as case labels the way VB's `Select Case` does, so this became an
  if/else-if chain comparing against `typeof(int)`/`typeof(uint)`/etc. instead.
- Confirmed (by reading `OpenAdmxFolder.vb`, `DownloadAdmx.vb`, `Main.vb` usage patterns
  during earlier files) that `PolicyPlus`-internal string literals with backslashes are
  never meant as regex/escape sequences — consistently used verbatim (`@"..."`) or
  explicitly-escaped (`"\\"`) C# strings throughout to preserve exact backslash counts,
  same care as in `PolicySource.cs` (1.4).

Still unverified by execution — same caveat as 1.4: build-clean confirms syntax/type
correctness, not runtime correctness. No test harness exists yet to actually load a real
ADMX/POL/REG file through this code path.

## [1.4] - Stage 2: ported the ADMX/ADML parsing core to C#

Ported `AdmlFile.vb` → `AdmlFile.cs`, `AdmxFile.vb` → `AdmxFile.cs` (the 20 KB core ADMX
parser), and `AdmxBundle.vb` → `AdmxBundle.cs` (merges multiple ADMX/ADML files into the
`PolicyPlusCategory`/`PolicyPlusPolicy`/etc. structures). These three were the biggest
source of forward-reference errors from batch 1 (1.2) — with them in, **all 13 ported
files now build clean**: `dotnet build -p:OutputType=Library` (bypassing the missing
WinForms entry point, expected until forms are ported) reports 0 warnings, 0 errors.

Notable translation decisions, since these files leaned heavily on `Option Strict Off`
implicit conversions that don't exist in C#:
- Several fields (`DecimalPolicyElement.Minimum`, `TextPolicyElement.MaxLength`,
  `CheckBoxPresentationElement.DefaultState`, etc.) were assigned directly from
  `AttributeOrDefault(...)`'s `object` return in VB, relying on implicit unboxing/
  conversion. Made explicit with `Convert.ToUInt32`/`Convert.ToInt32`/`Convert.ToBoolean`.
- `PolicyRegistryValue.NumberValue` (`UInteger`) and `AdmxProduct.Version`/
  `AdmxSupportEntry.MinVersion`/`MaxVersion` (`Integer`) were assigned directly from
  `XmlAttribute.Value` (a `String`) in VB. Made explicit with `uint.Parse`/`int.Parse`,
  using `CultureInfo.InvariantCulture` (ADMX attribute values are always plain
  ASCII digits, so this is equivalent to VB's runtime conversion behavior on any
  locale, and arguably more correct than relying on the current culture).
- `AdmxFile.vb`'s `entry.RegistryValue = ""` check (to decide whether to fall back to a
  `valueName` attribute) relied on VB's rule that `Nothing = ""` is `True` for strings.
  Translated to `string.IsNullOrEmpty(entry.RegistryValue)` to preserve that behavior
  instead of a literal `== ""` (which would differ on `null`).
- `categoryElement("parentCategory")` used VB's default-property indexer on `XmlNode`,
  which only resolves via late binding to `XmlElement.Item(string)` at runtime under
  `Option Strict Off`. Made explicit with a cast: `((XmlElement)categoryElement)["parentCategory"]`.
- `AdmxFile.vb`'s recursive `loadProducts` lambda (a `Dim ... As Action` referencing
  itself) became a C# local function, which supports natural recursion without the
  VB closure trick.
- VB's global `Split(Ref, ":", 2)` (the `Microsoft.VisualBasic.Strings` intrinsic, not
  `String.Split`) became `Ref.Split(new[] { ":" }, 2, StringSplitOptions.None)` —
  same "split at most once" semantics.

Still unverified by execution — no unit tests exist upstream and there's no ADMX sample
data wired up yet to actually parse. The build-clean result confirms type/syntax
correctness, not runtime correctness; that only gets checked once these are wired into a
running app (after the WinForms layer is ported) or a throwaway test harness parses a
real ADMX file.

## [1.3] - Stage 1 complete: retargeted to net10.0-windows (VB.NET)

Build-verified via `dotnet build` (both Debug and Release, both direct `.vbproj` and via
`PolicyPlus.sln`) — 0 errors. Not yet smoke-tested by launching the app (no interactive
Windows desktop in the working environment); that's the one remaining human step before
calling this fully done.

- Converted `PolicyPlus/PolicyPlus.vbproj` from the classic .NET Framework 4.5.2 project
  format to SDK-style, targeting **`net10.0-windows`**, `UseWindowsForms=true`. Kept
  `EnableDefaultItems=false` and the full curated `<Compile>`/`<EmbeddedResource>` item
  list (with `DependentUpon`/`SubType` metadata) instead of relying on SDK implicit
  globbing, so the WinForms designer's file nesting keeps working. Kept
  `GenerateAssemblyInfo=false` so the existing `AssemblyInfo.vb` stays authoritative. Kept
  `OptionStrict=Off`/`OptionExplicit=On`/`OptionCompare=Binary`/`OptionInfer=On` and the
  `StartupObject`/`ApplicationManifest`/`AssemblyName` ("Policy Plus", matching the CI
  artifact name) unchanged.
- Dropped `AutoGenerateBindingRedirects`, `FileAlignment`, `TargetFrameworkProfile` — all
  .NET-Framework-only concepts with no modern-.NET equivalent.
- Stopped referencing `App.config` from the project — it only declared a
  `<supportedRuntime>` for .NET Framework 4.5.2, which is meaningless (and wrong) on
  net10.0-windows. Left the file on disk, just unreferenced, rather than deleting it.
- `System.DirectoryServices`/`System.DirectoryServices.ActiveDirectory` (used by
  `OpenAdmxFolder.vb` to find the domain's central ADMX store) needed no package
  reference — the .NET 10 SDK reported it "automatically available" for
  `net10.0-windows`; an explicit `PackageReference` produced an `NU1100` restore error and
  was removed.
- Two real source fixes were required to compile under the modern VB/.NET toolchain (both
  behavior-preserving):
  - `FindByText.vb`: `cleanText.Split(" "c, vbCr, vbLf)` failed overload resolution
    (`vbCr`/`vbLf` are `String` constants; the modern compiler wouldn't narrow them to
    `Char` across a `ParamArray` the way the old one did). Swapped in
    `ControlChars.Cr`/`ControlChars.Lf`, which are `Char`-typed and represent the exact
    same characters.
  - `DownloadAdmx.vb`: `Directory.GetAccessControl`/`SetAccessControl` and
    `File.SetAccessControl` no longer exist as static `Directory`/`File` methods on
    modern .NET — they're extension methods on `DirectoryInfo`/`FileInfo` in
    `System.IO.FileSystemAclExtensions`. Added `Imports System.IO` and
    `Imports System.Security.AccessControl`, and changed call sites to construct a
    `DirectoryInfo`/`FileInfo` first, then call `.GetAccessControl()`/`.SetAccessControl()`
    on it.
- Updated `.github/workflows/latest.yml`: replaced `microsoft/setup-msbuild` +
  `msbuild.exe` with `actions/setup-dotnet@v4` (`10.0.x`) + `dotnet build -c Release`, and
  updated the two artifact-path references from `PolicyPlus\bin\Release\Policy Plus.exe`
  to `PolicyPlus\bin\Release\net10.0-windows\Policy Plus.exe` (SDK-style projects add a
  target-framework subfolder to the output path that the old format didn't have).
- ~1600 new `CA1416` "platform compatibility" warnings appeared (WinForms APIs flagged as
  Windows-only, which they always were — this is a new analyzer, not a regression). Left
  as-is for Stage 1; worth a follow-up pass to add `<SupportedOSPlatform>` and quiet these,
  but they don't block anything.
- **Not done yet**: launching the built exe and clicking through the UI. Needs a human (or
  a future session with an actual Windows desktop available) before Stage 1 is fully
  closed out per the plan's verification section.

## [1.2] - Started Stage 2: VB.NET → C# conversion (in progress)

- Created `PolicyPlusCs/` — a new SDK-style C# project (`net10.0-windows`, WinForms
  enabled) that will eventually replace `PolicyPlus/` (the VB.NET project). Not yet wired
  into `PolicyPlus.sln` and not build-verified — no .NET SDK was available in the working
  environment at the time of this port, so translation was done by careful manual
  read-through rather than a compile/fix loop. **Treat as unverified until a
  `dotnet build` pass is run.**
- Ported first batch: the smallest, most self-contained, non-UI files (no WinForms
  designer dependency), chosen to validate the file-by-file conversion approach at low
  risk before tackling the larger core-logic and UI files:
  - `BitReinterpretation.vb` → `BitReinterpretation.cs`
  - `XmlExtensions.vb` → `XmlExtensions.cs`
  - `PInvoke.vb` → `PInvoke.cs` (VB `Declare ... Lib` statements converted to
    `[DllImport]` externs; `Unicode`-declared functions mapped to `CharSet.Unicode`)
  - `SystemInfo.vb` → `SystemInfo.cs`
  - `Privilege.vb` → `Privilege.cs` (VB `Class` with only a shared method converted to a
    C# `static class` — same call surface, no behavior change)
  - `ConfigurationStorage.vb` → `ConfigurationStorage.cs`
  - `AdmxStructures.vb` → `AdmxStructures.cs`
  - `PolicyStructures.vb` → `PolicyStructures.cs`
  - `PresentationStructures.vb` → `PresentationStructures.cs`
  - `CompiledStructures.vb` → `CompiledStructures.cs`
- **Not yet portable / still references VB types**: `CompiledStructures.cs` and
  `AdmxStructures.cs` reference `AdmxFile` (defined in `AdmxFile.vb`, not yet ported —
  it's the largest core-logic file at 20 KB). The C# project will not compile until that
  file (and its siblings `AdmxBundle.vb`, `AdmlFile.vb`) are ported too. This is expected
  for a WIP multi-file port and is the next chunk of Stage 2 work.

## [1.1] - Local clone + modernization roadmap

- Cloned the fork locally to `PolicyPlus/` for direct file access (previously worked from
  GitHub API reads only).
- Established a two-stage modernization plan:
  - **Stage 1** (not started): retarget `PolicyPlus.vbproj` from .NET Framework 4.5.2 /
    classic project format to an SDK-style project on `net10.0-windows`, keeping VB.NET.
    Motivated directly by two upstream issues that are hard on the old framework and much
    easier on modern WinForms: [#78 HiDPI support](https://github.com/Fleex255/PolicyPlus/issues/78)
    and [#73 Dark Mode](https://github.com/Fleex255/PolicyPlus/issues/73).
  - **Stage 2** (started in 1.2 above): convert VB.NET → C# for a larger contributor pool
    and better tooling support, once Stage 1 is verified working.
- Deferred triaging the 18 open upstream issues until after Stage 1 lands, since some of
  the "quick win" issues are cheaper to fix post-modernization.

## [1.0] - Fork baseline

- Forked from [Fleex255/PolicyPlus](https://github.com/Fleex255/PolicyPlus) at commit
  `a2a5379` ("Fix text search for lines' last word").
- No code changes yet — baseline for tracking this fork's changes going forward.
