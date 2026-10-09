@echo off
setlocal enabledelayedexpansion
title Cong cu Git Auto Push - Antigravity AI
cls

:: Thiet lap mau sac (Nen den, chu trang)
color 0F

echo =======================================================================
echo          CONG CU TU DONG DAY CODE LEN GITHUB (TUAN THU .GITIGNORE)
echo =======================================================================
echo.

:: 1. Kiem tra xem Git da duoc cai dat chua
where git >nul 2>nul
if %errorlevel% neq 0 (
    color 0C
    echo [LOI] Git chua duoc cai dat tren may tinh cua ban!
    echo Vui long tai va cai dat Git tai: https://git-scm.com/
    echo Sau khi cai dat xong, hay chay lai file script nay.
    echo.
    pause
    exit /b
)

:: 2. Kiem tra xem thu muc hien tai da duoc khoi tao Git chua
if not exist .git (
    echo [THONG BAO] Thu muc .git chua ton tai. Dang khoi tao Git Repository...
    git init
    if %errorlevel% neq 0 (
        color 0C
        echo [LOI] Khong the khoi tao Git Repository.
        pause
        exit /b
    )
    rem Thiet lap nhanh mac dinh la main
    git branch -M main
    echo Khoi tao Git thanh cong va dat ten nhanh mac dinh la 'main'.
    echo.
)

:: 3. Kiem tra xem da cau hinh remote origin chua
git remote get-url origin >nul 2>nul
if %errorlevel% neq 0 (
    color 0E
    echo [THONG BAO] Chua tim thay lien ket voi kho chua [Remote GitHub].
    echo Vui long truy cap GitHub, tao mot repository moi [trong].
    echo Sau do copy URL cua repository do [dang https://github.com/user/repo.git].
    echo.
    set /p "remote_url=Nhap URL repository GitHub cua ban: "
    if not "!remote_url!"=="" (
        git remote add origin !remote_url!
        echo.
        echo [THANH CONG] Da lien ket voi repository GitHub thanh cong!
        color 0F
    ) else (
        color 0C
        echo [LOI] Ban phai cung cap URL Repository de co the day code len GitHub.
        echo.
        pause
        exit /b
    )
    echo.
)

:: Lay ten nhanh hien tai
for /f "tokens=*" %%i in ('git branch --show-current') do set "current_branch=%%i"
if "%current_branch%"=="" (
    rem Neu chua co commit nao, git branch --show-current co the rong, dat mac dinh la main
    set "current_branch=main"
    git branch -M main 2>nul
)

echo.
echo =======================================================================
echo  Buoc 1: Quet va chuan bi cac file thay doi (Tuan thu .gitignore)
echo =======================================================================
:: Chay git add . (tu dong loai tru cac file trong .gitignore)
git add .

echo Cac file se duoc them va day len GitHub (khong bao gom cac file bi ignore):
echo -----------------------------------------------------------------------
git status -s
echo -----------------------------------------------------------------------
echo.

:: 4. Nhap Commit Message
echo =======================================================================
echo  Buoc 2: Nhap thong tin Commit (Thong diep mo ta thay doi)
echo =======================================================================
set "commit_msg="
set /p "commit_msg=Nhap noi dung commit (Nhan Enter de dung mac dinh: 'Update code'): "
if "!commit_msg!"=="" (
    set "commit_msg=Update code [%date% %time%]"
)

:: Thuc hien commit
echo.
echo Dang thuc hien commit voi thong diep: "!commit_msg!"...
git commit -m "!commit_msg!"
echo.

:: 5. Day code len GitHub
echo =======================================================================
echo  Buoc 3: Tien hanh day code len GitHub (Nhanh: !current_branch!)
echo =======================================================================
echo Dang day du lieu len GitHub, vui long cho...
echo.

:: Thu day code len. Su dung -u origin de theo doi nhanh
git push -u origin !current_branch!

if %errorlevel% eq 0 (
    color 0A
    echo.
    echo =======================================================================
    echo [THANH CONG] Code da duoc day len GitHub thanh cong!
    echo Nhanh hoat dong: !current_branch!
    echo =======================================================================
) else (
    color 0C
    echo.
    echo =======================================================================
    echo [LOI] Day code len GitHub that bai.
    echo.
    echo Goi y xu ly loi:
    echo 1. Hay chac chan rang ban da ket noi Internet.
    echo 2. Kiem tra xem quyen truy cap GitHub cua ban da dung chua [SSH hoac HTTPS Credentials].
    echo 3. Neu repository GitHub co san cac file [nhu README, LICENSE] ma may cua ban chua co,
    echo    hay chay lenh 'git pull origin !current_branch! --rebase' truoc roi thu lai.
    echo =======================================================================
)

echo.
pause
