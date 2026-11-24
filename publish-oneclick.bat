@echo off
chcp 65001 >nul
setlocal EnableDelayedExpansion

echo ============================================================
echo   XiaoGouYingYu - One-click publish to GitHub
echo   You do NOT need to compile anything. GitHub does it for you.
echo ============================================================
echo.

where git >nul 2>nul
if errorlevel 1 (
    echo [ERROR] Git is not installed on this computer.
    echo Please download and install Git for Windows first:
    echo     https://git-scm.com/download/win
    echo Then run this script again.
    echo.
    pause
    exit /b 1
)

set "REPO_URL="
set /p REPO_URL= Paste your GitHub repo URL (https://github.com/xxx/xxx.git): 
if "%REPO_URL%"=="" (
    echo [ERROR] No repository URL entered. Nothing was changed.
    echo.
    pause
    exit /b 1
)

cd /d "%~dp0"

echo.
echo [1/5] Preparing local repository ...
if not exist ".git" (
    git init
    git config user.name "XiaoGouYingYu Builder"
    git config user.email "builder@example.com"
    git branch -M main
    echo   Local repository created.
) else (
    echo   Local repository already exists, reusing it.
)

echo [2/5] Staging files ...
git add .
if errorlevel 1 (
    echo [ERROR] git add failed. See message above.
    pause
    exit /b 1
)

echo [3/5] Committing ...
git commit -m "v1.0.0 first release"
if errorlevel 1 (
    echo   NOTE: nothing new to commit, or commit failed. Continuing.
)

echo [4/5] Connecting to GitHub ...
git remote get-url origin >nul 2>nul
if errorlevel 1 (
    git remote add origin %REPO_URL%
) else (
    git remote set-url origin %REPO_URL%
)
git branch -M main

echo   Pushing source code (please wait, first push may take a while) ...
git push -u origin main
if errorlevel 1 (
    echo.
    echo [ERROR] Push failed. Common reasons:
    echo   1. The repository URL is wrong
    echo   2. GitHub login / token dialog was cancelled
    echo   3. The remote repository is NOT empty ^(has README/LICENSE^)
    echo Fix the issue and run this script again.
    echo.
    pause
    exit /b 1
)

echo [5/5] Tagging v1.0.0 to trigger the installer build ...
git tag v1.0.0 2>nul
git push origin v1.0.0
if errorlevel 1 (
    echo   NOTE: tag push failed. If v1.0.0 already exists, that is fine.
)

echo.
echo ============================================================
echo   DONE. What happens next is automatic:
echo   1. Open your repo on GitHub -^> "Actions" tab
echo   2. Wait for the workflow to finish (green check mark)
echo   3. Open "Releases" -^> download Setup.exe
echo   4. Send Setup.exe to the parents. They just double-click it.
echo.
echo   If the workflow shows a RED cross, copy the error log
echo   and send it back. I will fix the code in the workspace.
echo ============================================================
echo.
pause
