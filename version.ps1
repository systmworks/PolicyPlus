$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

# Embed the current commit into Version.cs, shown in the app's About dialog.
$gitVer = (git -C $root describe --always).Trim()
$versionCsPath = Join-Path $root "PolicyPlus\Version.cs"
@"
// DO NOT MODIFY THIS FILE. To update it, run version.bat again.
namespace PolicyPlus
{
    static class VersionHolder
    {
        public const string Version = "$gitVer";
    }
}
"@ | Set-Content -Path $versionCsPath -NoNewline

# Keep the compiled EXE's File/Product version (Windows file properties) in sync with the fork's
# current CHANGELOG version, so it never goes stale the way the old hardcoded 1.0.0.0 did.
$changelogPath = Join-Path $root "CHANGELOG.md"
$match = Select-String -Path $changelogPath -Pattern '^## \[(?<v>\d+\.\d+)\]' | Select-Object -First 1
if (-not $match) { throw "Could not find a version header (## [X.Y]) at the top of CHANGELOG.md" }
$forkVersion = "$($match.Matches[0].Groups['v'].Value).0.0"

$assemblyInfoPath = Join-Path $root "PolicyPlus\My Project\AssemblyInfo.cs"
(Get-Content $assemblyInfoPath) | ForEach-Object {
    $_ -replace 'AssemblyVersion\("[\d.]+"\)', "AssemblyVersion(`"$forkVersion`")" `
       -replace 'AssemblyFileVersion\("[\d.]+"\)', "AssemblyFileVersion(`"$forkVersion`")"
} | Set-Content -Path $assemblyInfoPath

Write-Host "Embedded commit $gitVer into Version.cs and version $forkVersion into AssemblyInfo.cs"
