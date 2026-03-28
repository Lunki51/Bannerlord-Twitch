@echo off
title BLT Overlay Server
cd /d "%~dp0"

:: ── Locate Node.js ────────────────────────────────────────────
set "NODE_EXE="

:: Try PATH first
where node >nul 2>&1
if not errorlevel 1 set "NODE_EXE=node"

:: Fallback to common locations if PATH failed
if "%NODE_EXE%"=="" if exist "C:\Program Files\nodejs\node.exe" set "NODE_EXE=C:\Program Files\nodejs\node.exe"
if "%NODE_EXE%"=="" if exist "C:\Program Files (x86)\nodejs\node.exe" set "NODE_EXE=C:\Program Files (x86)\nodejs\node.exe"
if "%NODE_EXE%"=="" if exist "%LOCALAPPDATA%\Programs\nodejs\node.exe" set "NODE_EXE=%LOCALAPPDATA%\Programs\nodejs\node.exe"

if "%NODE_EXE%"=="" (
    echo.
    echo  *** MISSING: Node.js ***
    echo  Node.js was not found on PATH or in common install locations.
    echo  Download it from: https://nodejs.org (click the LTS button)
    echo.
    pause
    exit /b 1
)

echo [OK] Node.js found: %NODE_EXE%
"%NODE_EXE%" --version

:: ── Locate npm ───────────────────────────────────────────────
where npm >nul 2>&1
if errorlevel 1 (
    echo.
    echo  *** MISSING: npm ***
    echo  npm was not found on your PATH.
    echo  Try reinstalling Node.js from: https://nodejs.org
    echo.
    pause
    exit /b 1
) else (
    set "NPM_CMD=npm"
    echo [OK] npm found: %NPM_CMD%
    "%NPM_CMD%" --version
)

:: ── Ensure server.js exists ───────────────────────────────────
if not exist "server.js" (
    echo.
    echo  *** ERROR: server.js not found ***
    echo  Make sure this file is in the same folder as this script.
    echo.
    pause
    exit /b 1
)

:: ── Install dependencies if needed ────────────────────────────
if not exist "node_modules" (
    echo.
    echo Installing dependencies, please wait...
    call "%NPM_CMD%" install
    if errorlevel 1 (
        echo.
        echo  npm install failed. Check your internet connection and try again.
        echo.
        pause
        exit /b 1
    )
)

:: ── Create public folder if missing ──────────────────────────
if not exist "public" mkdir public

:: ── Warn if overlay HTML is missing ───────────────────────────
dir /b "public\*.html" >nul 2>&1
if errorlevel 1 (
    echo.
    echo  WARNING: No HTML file found in public\
    echo  Copy your overlay HTML file into the public\ folder.
    echo.
)

:: ── Start server ──────────────────────────────────────────────
echo.
echo  Starting BLT Overlay Server...
echo  Press Ctrl+C to stop.
echo.

:: Run Node and capture exit code
"%NODE_EXE%" server.js
set SERVER_EXIT=%ERRORLEVEL%

if %SERVER_EXIT% neq 0 (
    echo.
    echo  *** Server exited with error code %SERVER_EXIT% ***
    echo  Check server.js and dependencies.
    echo.
    pause
) else (
    echo.
    echo  Server exited normally.
    pause
)