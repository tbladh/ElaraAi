@echo off
setlocal
set SCENARIO=%~1
if "%SCENARIO%"=="" set SCENARIO=session
"%~dp0Elara.Host.exe" --record=%SCENARIO%
endlocal
