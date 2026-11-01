@echo off
setlocal enabledelayedexpansion

echo ===================================================
echo Building C++ Remote Control Server (Windows x64)
echo ===================================================

if not exist "bin" mkdir bin

:: Find vcvarsall.bat
set "VCVARS="
if exist "C:\Program Files\Microsoft Visual Studio\18\Insiders\VC\Auxiliary\Build\vcvarsall.bat" (
    set "VCVARS=C:\Program Files\Microsoft Visual Studio\18\Insiders\VC\Auxiliary\Build\vcvarsall.bat"
) else if exist "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvarsall.bat" (
    set "VCVARS=C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvarsall.bat"
) else if exist "C:\Program Files\Microsoft Visual Studio\2022\Professional\VC\Auxiliary\Build\vcvarsall.bat" (
    set "VCVARS=C:\Program Files\Microsoft Visual Studio\2022\Professional\VC\Auxiliary\Build\vcvarsall.bat"
) else if exist "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\VC\Auxiliary\Build\vcvarsall.bat" (
    set "VCVARS=C:\Program Files\Microsoft Visual Studio\2022\Enterprise\VC\Auxiliary\Build\vcvarsall.bat"
)

:: If cl is not already in path, initialize MSVC
where cl >nul 2>nul
if %ERRORLEVEL% neq 0 (
    if defined VCVARS (
        echo [INFO] Initializing Visual Studio environment...
        call "!VCVARS!" x64 >nul
    ) else (
        echo [ERROR] MSVC compiler environment not found. Please run from VS Developer Command Prompt.
        exit /b 1
    )
)

echo [INFO] Compiling RemoteControlServer.exe...
cl.exe /nologo /std:c++17 /EHsc /O2 /W3 /Iinclude ^
    src\main.cpp ^
    src\NetworkServer.cpp ^
    src\CommandDispatcher.cpp ^
    src\InputController.cpp ^
    src\NetworkUtils.cpp ^
    /Fe:bin\RemoteControlServer.exe ^
    /link ws2_32.lib user32.lib iphlpapi.lib PowrProf.lib

if %ERRORLEVEL% equ 0 (
    echo.
    echo ===================================================
    echo [SUCCESS] Build succeeded: bin\RemoteControlServer.exe
    echo ===================================================
    :: Clean up intermediate obj files
    del *.obj 2>nul
    exit /b 0
) else (
    echo.
    echo [ERROR] Build failed with error code %ERRORLEVEL%.
    exit /b %ERRORLEVEL%
)
