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
    echo  Download it from: https://nodejs.org
    echo.
    pause
    exit /b 1
)

echo [OK] Node.js found: %NODE_EXE%
call "%NODE_EXE%" --version

:: ── Locate npm ───────────────────────────────────────────────
where npm >nul 2>&1
if errorlevel 1 (
    echo.
    echo  *** MISSING: npm ***
    echo  npm was not found on your PATH.
    echo.
    pause
    exit /b 1
) else (
    set "NPM_CMD=npm"
    echo [OK] npm found: %NPM_CMD%
    call "%NPM_CMD%" --version
)

:: ── Ensure server.js exists ───────────────────────────────────
if not exist "server.js" (
    echo.
    echo  *** ERROR: server.js not found ***
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
        echo  npm install failed.
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
    echo.
)

:: ── Start server ──────────────────────────────────────────────
echo.
echo  Starting BLT Overlay Server...
echo  Press Ctrl+C to stop.
echo.

:: IMPORTANT: use CALL so batch waits properly
call "%NODE_EXE%" server.js 2>&1

set SERVER_EXIT=%ERRORLEVEL%

echo.
if %SERVER_EXIT% neq 0 (
    echo  *** Server exited with error code %SERVER_EXIT% ***
) else (
    echo  Server exited normally.
)

echo.
pause