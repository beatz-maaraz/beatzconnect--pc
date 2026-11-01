@echo off
setlocal enabledelayedexpansion

echo =====================================================================
echo Beatz Control -- Qt Installer Framework Build Pipeline
echo =====================================================================

set SCRIPT_DIR=%~dp0
set QT_APP_DIR=%SCRIPT_DIR%..
set BUILD_DIR=%QT_APP_DIR%\build
set DATA_DIR=%SCRIPT_DIR%packages\com.beatzcontrol.app\data
set CONFIG_XML=%SCRIPT_DIR%config\config.xml
set PACKAGES_DIR=%SCRIPT_DIR%packages
set OUTPUT_EXE=%SCRIPT_DIR%BeatzControl_Setup_1.23.50.exe

:: 1. Verify build executable exists
if not exist "%BUILD_DIR%\BeatzControl.exe" (
    echo [ERROR] Build target not found at %BUILD_DIR%\BeatzControl.exe
    echo Please run CMake configure and build first:
    echo   cmake -B build -S .
    echo   cmake --build build --config Release
    exit /b 1
)

:: 2. Prepare package data directory
echo [1/4] Preparing package data staging directory...
if exist "%DATA_DIR%" rd /s /q "%DATA_DIR%"
mkdir "%DATA_DIR%"

:: 3. Copy application binary and assets
echo [2/4] Copying application binaries...
copy "%BUILD_DIR%\BeatzControl.exe" "%DATA_DIR%\" >nul
copy "%QT_APP_DIR%\assets\icon.ico" "%DATA_DIR%\" >nul

:: 4. Deploy Qt runtime dependencies using windeployqt
echo [3/4] Running windeployqt for runtime bundling...
where windeployqt.exe >nul 2>nul
if %errorlevel% equ 0 (
    windeployqt.exe --qmldir "%QT_APP_DIR%\qml" --release "%DATA_DIR%\BeatzControl.exe"
) else (
    echo [WARNING] windeployqt.exe not found in PATH. Ensure Qt bin directory is in PATH.
)

:: 5. Generate Windows installer using Qt Installer Framework binarycreator
echo [4/4] Creating Windows Installer with Qt Installer Framework...
where binarycreator.exe >nul 2>nul
if %errorlevel% equ 0 (
    binarycreator.exe -c "%CONFIG_XML%" -p "%PACKAGES_DIR%" "%OUTPUT_EXE%"
    echo.
    echo [SUCCESS] Windows Installer created successfully:
    echo %OUTPUT_EXE%
) else (
    echo [NOTICE] binarycreator.exe not found in PATH.
    echo If Qt Installer Framework is installed in a custom location, specify its bin directory:
    echo Example: "C:\Qt\QtIFW-4.6.0\bin\binarycreator.exe" -c "%CONFIG_XML%" -p "%PACKAGES_DIR%" "%OUTPUT_EXE%"
)

echo.
echo Process complete.

