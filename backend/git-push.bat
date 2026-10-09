@echo off
chcp 65001 >nul
title ELIBAPI - Push to GitHub

echo ========================================
echo   ELIBAPI - Push code to GitHub
echo ========================================
echo.

cd /d "%~dp0"

echo [1/4] Checking status...
git status -s
echo.

set /p MSG="Commit message: "
if "%MSG%"=="" (
    echo No commit message entered. Cancelled.
    pause
    exit /b 1
)

echo.
echo [2/4] Adding all changes...
git add -A

echo [3/4] Committing...
git commit -m "%MSG%"

echo [4/4] Pushing to GitHub (origin/master)...
git push origin master

echo.
if %ERRORLEVEL%==0 (
    echo Push completed successfully!
) else (
    echo Push failed! Check the error above.
)

echo.
pause
