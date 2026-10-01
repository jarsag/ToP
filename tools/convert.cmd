@echo off
rem Runs the converter without opening a terminal by hand. Takes the same
rem arguments as tools\convert.ps1, or none at all to get the wizard.
setlocal

set "SHELL=pwsh"
where pwsh >nul 2>nul || set "SHELL=powershell"

"%SHELL%" -NoProfile -ExecutionPolicy Bypass -File "%~dp0convert.ps1" %*
exit /b %ERRORLEVEL%
