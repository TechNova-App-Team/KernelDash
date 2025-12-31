@echo off
REM KernelDash Build & Deploy Script

echo ========================================
echo KernelDash - Build Release
echo ========================================

REM Build
echo.
echo [1/3] Kompiliere Projekt...
dotnet build -c Release

if errorlevel 1 (
    echo Fehler beim Build!
    pause
    exit /b 1
)

REM Publish
echo.
echo [2/3] Veroeffentliche Release...
dotnet publish -c Release -o ./publish --self-contained

if errorlevel 1 (
    echo Fehler beim Publish!
    pause
    exit /b 1
)

REM Create Desktop Shortcut (optional)
echo.
echo [3/3] Erstelle Desktop-Shortcut...

setlocal enabledelayedexpansion
set PUBLISH_PATH=%CD%\publish\KernelDash.exe
set SHORTCUT_PATH=%USERPROFILE%\Desktop\KernelDash.lnk

REM Versuche mit VBScript einen Shortcut zu erstellen
(
    echo Set oWS = WScript.CreateObject("WScript.Shell"^)
    echo sLinkFile = "%SHORTCUT_PATH%"
    echo Set oLink = oWS.CreateShortcut(sLinkFile^)
    echo oLink.TargetPath = "%PUBLISH_PATH%"
    echo oLink.WorkingDirectory = "%CD%\publish"
    echo oLink.Description = "KernelDash System Manager"
    echo oLink.Save
) > CreateShortcut.vbs

cscript.exe CreateShortcut.vbs
del CreateShortcut.vbs

echo.
echo ========================================
echo ^ Build erfolgreich!
echo ========================================
echo.
echo Release-Pfad: %CD%\publish
echo Startdatei: %PUBLISH_PATH%
echo Desktop-Shortcut: %SHORTCUT_PATH%
echo.
echo Sie koennen die Anwendung jetzt starten:
echo   - Doppelklick auf Desktop-Shortcut (KernelDash.lnk^)
echo   - Oder: %PUBLISH_PATH%
echo.
pause

