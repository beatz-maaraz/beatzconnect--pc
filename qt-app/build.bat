@echo off
setlocal enabledelayedexpansion

echo =========================================================================
echo Beatz Control -- Qt C++ Application Build Tool
echo =========================================================================

set SCRIPT_DIR=%~dp0
cd /d "%SCRIPT_DIR%"

:: Look for Visual Studio CMake if not in system PATH
where cmake.exe >nul 2>nul
if %errorlevel% neq 0 (
    if exist "C:\Program Files\Microsoft Visual Studio\18\Insiders\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" (
        set "PATH=C:\Program Files\Microsoft Visual Studio\18\Insiders\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin;%PATH%"
    ) else if exist "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" (
        set "PATH=C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin;%PATH%"
    )
)

:: Optional user-specified Qt directory argument or search default locations
set "QT_DIR_ARG=%~1"
if not "%QT_DIR_ARG%"=="" (
    set "CMAKE_QT_PREFIX=-DCMAKE_PREFIX_PATH=%QT_DIR_ARG%"
    echo Using custom Qt prefix path: %QT_DIR_ARG%
) else if exist "C:\Qt\6.6.3\msvc2019_64" (
    set "CMAKE_QT_PREFIX=-DCMAKE_PREFIX_PATH=C:\Qt\6.6.3\msvc2019_64"
) else if exist "C:\Qt\6.5.3\msvc2019_64" (
    set "CMAKE_QT_PREFIX=-DCMAKE_PREFIX_PATH=C:\Qt\6.5.3\msvc2019_64"
) else if exist "C:\Qt\5.15.2\msvc2019_64" (
    set "CMAKE_QT_PREFIX=-DCMAKE_PREFIX_PATH=C:\Qt\5.15.2\msvc2019_64"
)

echo.
echo [1/3] Configuring with CMake...
cmake -B build -S . %CMAKE_QT_PREFIX%
if %errorlevel% neq 0 (
    echo.
    echo [NOTE] If Qt is installed in a custom directory, pass it as an argument:
    echo        build.bat "C:\Qt\6.6.3\msvc2019_64"
    exit /b %errorlevel%
)

echo.
echo [2/3] Compiling Beatz Control Qt C++ executable...
cmake --build build --config Release
if %errorlevel% neq 0 (
    echo [ERROR] Build failed.
    exit /b %errorlevel%
)

echo.
echo [3/3] Packaging installer with Qt Installer Framework...
if exist "installer\build_installer.bat" (
    call "installer\build_installer.bat"
)

echo.
echo =========================================================================
echo Build complete.
echo Executable: %SCRIPT_DIR%build\BeatzControl.exe
echo =========================================================================
