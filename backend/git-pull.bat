@echo off
chcp 65001 >nul
title ELIBAPI - Pull from GitHub

echo ========================================
echo   ELIBAPI - Pull code from GitHub
echo ========================================
echo.

cd /d "%~dp0"

echo [1/2] Fetching from GitHub...
git fetch origin

echo.
echo [2/2] Pulling latest code (origin/master)...
git pull origin master

echo.
if %ERRORLEVEL%==0 (
    echo Pull completed successfully!
) else (
    echo Pull failed! Check the error above.
)

echo.
pause
