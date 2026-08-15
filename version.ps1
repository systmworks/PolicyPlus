$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

$gitVer = (git -C $root describe --always).Trim()

# Single source of truth for the app's own version number: the "## [X.Y]" header at the top
# of CHANGELOG.md. Every displayed version (Windows file properties, title bar, About
# dialog) is derived from this one parsed value - never set independently anywhere else.
$changelogPath = Join-Path $root "CHANGELOG.md"
$match = Select-String -Path $changelogPath -Pattern '^## \[(?<v>\d+\.\d+)\]' | Select-Object -First 1
if (-not $match) { throw "Could not find a version header (## [X.Y]) at the top of CHANGELOG.md" }
$appVersion = $match.Matches[0].Groups['v'].Value
$fileVersion = "$appVersion.0.0"

# Embed the current commit and the app version into Version.cs, read by Main.cs for the
# title bar and About dialog.
$versionCsPath = Join-Path $root "PolicyPlus\Version.cs"
@"
// DO NOT MODIFY THIS FILE. To update it, run version.bat again.
namespace PolicyPlus
{
    static class VersionHolder
    {
        public const string Version = "$gitVer";
        public const string AppVersion = "$appVersion";
    }
}
"@ | Set-Content -Path $versionCsPath -NoNewline

# Keep the compiled EXE's File/Product version (Windows file properties) in sync too, so it
# never goes stale the way the old hardcoded 1.0.0.0 did.
$assemblyInfoPath = Join-Path $root "PolicyPlus\My Project\AssemblyInfo.cs"
(Get-Content $assemblyInfoPath) | ForEach-Object {
    $_ -replace 'AssemblyVersion\("[\d.]+"\)', "AssemblyVersion(`"$fileVersion`")" `
       -replace 'AssemblyFileVersion\("[\d.]+"\)', "AssemblyFileVersion(`"$fileVersion`")"
} | Set-Content -Path $assemblyInfoPath

Write-Host "Embedded commit $gitVer and app version $appVersion into Version.cs; $fileVersion into AssemblyInfo.cs"
