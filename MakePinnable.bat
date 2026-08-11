@echo off
setlocal EnableExtensions

REM Installs Job Application Tracker into your user profile and creates
REM Start Menu + Desktop shortcuts so you can pin it like a normal app.

set "SOURCE=%~dp0publish\JobApplicationTracker.exe"
if not exist "%SOURCE%" set "SOURCE=%~dp0JobApplicationTracker.exe"
if not exist "%SOURCE%" set "SOURCE=%~dp0JobApplicationTracker\publish\JobApplicationTracker.exe"

if not exist "%SOURCE%" (
  echo.
  echo Could not find JobApplicationTracker.exe
  echo.
  echo First build it with:
  echo   cd /d "%~dp0JobApplicationTracker"
  echo   dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\publish
  echo.
  echo Then run this file again.
  echo.
  pause
  exit /b 1
)

set "INSTALL_DIR=%LOCALAPPDATA%\JobApplicationTracker\App"
set "START_MENU=%APPDATA%\Microsoft\Windows\Start Menu\Programs"
set "DESKTOP=%USERPROFILE%\Desktop"
set "SHORTCUT_NAME=Job Application Tracker.lnk"
set "EXE_DEST=%INSTALL_DIR%\JobApplicationTracker.exe"

echo.
echo Installing to:
echo   %INSTALL_DIR%
echo.

if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"

copy /Y "%SOURCE%" "%EXE_DEST%" >nul
if errorlevel 1 (
  echo Copy failed. Close the app if it is open and try again.
  pause
  exit /b 1
)

for %%F in ("%~dp0publish\*.dll") do copy /Y "%%~F" "%INSTALL_DIR%\" >nul 2>nul
for %%F in ("%~dp0JobApplicationTracker\publish\*.dll") do copy /Y "%%~F" "%INSTALL_DIR%\" >nul 2>nul

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$exe = '%EXE_DEST%';" ^
  "$start = Join-Path '%START_MENU%' '%SHORTCUT_NAME%';" ^
  "$desk = Join-Path '%DESKTOP%' '%SHORTCUT_NAME%';" ^
  "$ws = New-Object -ComObject WScript.Shell;" ^
  "foreach ($path in @($start, $desk)) {" ^
  "  $sc = $ws.CreateShortcut($path);" ^
  "  $sc.TargetPath = $exe;" ^
  "  $sc.WorkingDirectory = [System.IO.Path]::GetDirectoryName($exe);" ^
  "  $sc.WindowStyle = 1;" ^
  "  $sc.Description = 'Job Application Tracker';" ^
  "  $sc.IconLocation = ($exe + ',0');" ^
  "  $sc.Save();" ^
  "}"

if errorlevel 1 (
  echo Shortcut creation failed.
  pause
  exit /b 1
)

echo Done.
echo.
echo Shortcuts created:
echo   Start Menu: %START_MENU%\%SHORTCUT_NAME%
echo   Desktop:    %DESKTOP%\%SHORTCUT_NAME%
echo.
echo To pin it:
echo   1. Open Start and search for "Job Application Tracker"
echo   2. Right-click - Pin to Start
echo   3. Right-click - Pin to taskbar
echo.
echo Or right-click the Desktop shortcut and choose Pin to taskbar.
echo.
start "" "%EXE_DEST%"
pause
endlocal
