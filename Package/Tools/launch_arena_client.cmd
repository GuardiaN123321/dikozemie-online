@echo off
setlocal EnableExtensions
set "GAME_DIR=%BANNERLORD_GAME_DIR%"
if not defined GAME_DIR for %%G in ("D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord" "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord") do if exist "%%~G\bin\Win64_Shipping_Client\Bannerlord.exe" set "GAME_DIR=%%~G"
set "GAME_BIN=%GAME_DIR%\bin\Win64_Shipping_Client"
set "GAME_EXE=%GAME_BIN%\Bannerlord.exe"
set "SteamAppId=261550"

if not exist "%GAME_EXE%" (
  echo [ERROR] Bannerlord.exe was not found. Set BANNERLORD_GAME_DIR.
  exit /b 1
)
if not exist "%GAME_DIR%\Modules\NavalDLC\SubModule.xml" (
  echo [ERROR] War Sails is required for native naval battles.
  exit /b 2
)

if exist "%~dp0prepare_bannerlord_launch.js" (
  cscript.exe //nologo "%~dp0prepare_bannerlord_launch.js" >nul 2>&1
)

start "Arena" /D "%GAME_BIN%" "%GAME_EXE%" /multiplayer "_MODULES_*Native*SandBoxCore*CustomBattle*Sandbox*StoryMode*NavalDLC*Multiplayer*DikozemieOnline*_MODULES_" %*
echo [OK] Multiplayer arena launch requested.
exit /b 0
