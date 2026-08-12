@echo off
setlocal
set VF=PolicyPlus\Version.cs
for /f %%i in ('git describe --always') do set GITVER=%%i
echo // DO NOT MODIFY THIS FILE. To update it, run version.bat again. > %VF%
echo namespace PolicyPlus >> %VF%
echo { >> %VF%
echo     static class VersionHolder >> %VF%
echo     { >> %VF%
echo         public const string Version = "%GITVER%"; >> %VF%
echo     } >> %VF%
echo } >> %VF%
