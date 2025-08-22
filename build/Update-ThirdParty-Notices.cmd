@echo off
setlocal

REM Updates THIRD-PARTY-NOTICES.md in Elara.Host project directory.
REM It calls the PowerShell script without parameters, which defaults to Elara.Host
REM and writes to Elara.Host\THIRD-PARTY-NOTICES.md only.

set SCRIPT_DIR=%~dp0
set PS_SCRIPT=%SCRIPT_DIR%Generate-TPN.ps1

powershell -NoProfile -ExecutionPolicy Bypass -File "%PS_SCRIPT%"

endlocal
