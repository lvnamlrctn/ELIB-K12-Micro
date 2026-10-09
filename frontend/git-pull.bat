@echo off
chcp 65001 >nul
setlocal

REM ============================================================
REM  Lay code moi nhat tu GitHub ve (branch main)
REM  Repo: https://github.com/lvnamlrctn/admin-tvxa.git
REM ============================================================

set REPO_URL=https://github.com/lvnamlrctn/admin-tvxa.git
set BRANCH=main

REM Lam viec tai thu muc chua file .bat nay
cd /d "%~dp0"

REM Kiem tra da cai git chua
where git >nul 2>nul
if errorlevel 1 (
    echo [LOI] Chua cai Git. Tai tai: https://git-scm.com/download/win
    pause
    exit /b 1
)

if exist ".git" (
    echo === Dang cap nhat code tu GitHub (branch %BRANCH%) ===
    git fetch origin
    if errorlevel 1 goto :err
    git pull origin %BRANCH%
    if errorlevel 1 goto :err
) else (
    echo === Chua co repo - dang clone tu GitHub ===
    git clone -b %BRANCH% %REPO_URL% .
    if errorlevel 1 goto :err
)

echo.
echo === HOAN TAT: da lay code moi nhat ===
pause
exit /b 0

:err
echo.
echo [LOI] Khong the lay code. Kiem tra ket noi mang / dang nhap GitHub / thay doi cuc bo chua commit.
pause
exit /b 1
