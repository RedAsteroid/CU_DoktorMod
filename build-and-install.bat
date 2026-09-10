@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "PROJECT_DIR=%~dp0"
set "GAME_DIR=%~1"

if not defined GAME_DIR if defined CASUALTIES_UNKNOWN_DIR set "GAME_DIR=%CASUALTIES_UNKNOWN_DIR%"

if not defined GAME_DIR (
    for /f "usebackq delims=" %%I in (`powershell -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%find-game-dir.ps1"`) do (
        if not defined GAME_DIR set "GAME_DIR=%%I"
    )
)

if not defined GAME_DIR (
    echo Could not find Casualties Unknown Demo automatically.
    echo Pass the game folder path as the first argument, or set CASUALTIES_UNKNOWN_DIR.
    echo Example: build-and-install.bat "D:\SteamLibrary\steamapps\common\Casualties Unknown Demo"
    exit /b 1
)

if not exist "%GAME_DIR%\CasualtiesUnknown_Data\Managed\Assembly-CSharp.dll" (
    echo Invalid game folder: "%GAME_DIR%"
    echo Could not find CasualtiesUnknown_Data\Managed\Assembly-CSharp.dll.
    exit /b 1
)

if not exist "%GAME_DIR%\BepInEx\core\BepInEx.dll" (
    echo BepInEx was not found in "%GAME_DIR%\BepInEx\core".
    echo Install BepInEx first, then run this script again.
    exit /b 1
)

set "PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\DoktorMod"
set "RUNTIME_DLL=%GAME_DIR%\BepInEx\plugins\KefPluginRuntime.dll"

call "%PROJECT_DIR%..\KefPluginRuntime\build-and-install.bat" "%GAME_DIR%"
if errorlevel 1 (
    if exist "%RUNTIME_DLL%" (
        echo.
        echo KefPluginRuntime could not be refreshed, probably because the game is running.
        echo Continuing because "%RUNTIME_DLL%" already exists.
        echo Close the game and rerun this script later if you want to update the shared runtime.
        echo.
    ) else (
        exit /b 1
    )
)

dotnet build "%PROJECT_DIR%DoktorMod.csproj" -c Release -p:GameDir="%GAME_DIR%"
if errorlevel 1 exit /b 1

if not exist "%PLUGIN_DIR%" mkdir "%PLUGIN_DIR%"

copy /Y "%PROJECT_DIR%bin\Release\netstandard2.1\DoktorMod.dll" "%PLUGIN_DIR%\DoktorMod.dll"
if errorlevel 1 (
    echo.
    echo Could not copy DoktorMod.dll into the BepInEx plugins folder.
    echo If the game is running, close it first. Windows locks plugin DLLs while BepInEx has them loaded.
    echo.
    for /f "usebackq delims=" %%I in (`powershell -NoProfile -Command "$p = Get-Process | Where-Object { $_.Path -like '*Casualties Unknown Demo*' -or $_.ProcessName -like '*Casualties*' -or $_.ProcessName -like '*Scav*' } | Select-Object -First 1; if ($p) { $p.Id }"`) do (
        set "LOCKING_PID=%%I"
    )
    if not "!LOCKING_PID!" == "" (
        echo Queuing deferred DoktorMod install for after game process !LOCKING_PID! exits.
        powershell -NoProfile -Command "$q=[char]34; $a=@('-NoProfile','-ExecutionPolicy','Bypass','-File',$q+'%PROJECT_DIR%install-after-exit.ps1'+$q,'-ProcessId','!LOCKING_PID!','-SourcePath',$q+'%PROJECT_DIR%bin\Release\netstandard2.1\DoktorMod.dll'+$q,'-DestinationPath',$q+'%PLUGIN_DIR%\DoktorMod.dll'+$q,'-LogPath',$q+'%PROJECT_DIR%install-after-exit.log'+$q); Start-Process -WindowStyle Hidden powershell -ArgumentList $a"
        echo Deferred install queued. Check "%PROJECT_DIR%install-after-exit.log" after closing the game.
        exit /b 0
    )
    echo Could not identify the locking game process, so deferred install was not queued.
    exit /b 1
)

echo Installed DoktorMod.dll to "%PLUGIN_DIR%"
