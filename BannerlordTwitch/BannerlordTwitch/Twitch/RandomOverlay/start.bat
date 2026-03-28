@echo off
title BLT Overlay Server
cd /d "%~dp0"

:: Refresh PATH from registry so a freshly installed Node.js is found
:: without needing to open a new command prompt window
for /f "tokens=2*" %%a in ('reg query "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v Path 2^>nul') do set "PATH=%%b;%PATH%"
for /f "tokens=2*" %%a in ('reg query "HKCU\Environment" /v Path 2^>nul') do set "PATH=%%b;%PATH%"

:: Check Node.js
where node >nul 2>&1
if %errorlevel% neq 0 (
    echo.
    echo  MISSING: Node.js is not installed.
    echo  Download it from: https://nodejs.org  (click the LTS button)
    echo  Run the installer, click through the defaults, then run this file again.
    echo.
    pause
    exit /b 1
)

:: Check npm (should come with Node but occasionally breaks)
where npm >nul 2>&1
if %errorlevel% neq 0 (
    echo.
    echo  MISSING: npm is not installed (it normally comes with Node.js).
    echo  Try reinstalling Node.js from: https://nodejs.org
    echo  Run the installer, click through the defaults, then run this file again.
    echo.
    pause
    exit /b 1
)

:: Install dependencies if needed
if not exist "node_modules" (
    echo Installing dependencies, please wait...
    call npm install
    if %errorlevel% neq 0 (
        echo npm install failed. Check your internet connection.
        pause
        exit /b 1
    )
)

:: Create public dir if missing
if not exist "public" mkdir public

:: Warn if ting.html isn't in place
if not exist "public\ting.html" (
    echo.
    echo  WARNING: public\ting.html not found.
    echo  Copy ting.html into the public\ folder.
    echo.
)

echo.
echo  Starting BLT Overlay Server...
echo  Press Ctrl+C to stop.
echo.
node server.js
pause