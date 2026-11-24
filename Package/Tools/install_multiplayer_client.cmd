@echo off
setlocal EnableExtensions

set "GAME_DIR=%BANNERLORD_GAME_DIR%"
rem The archive may be unpacked right into the game folder.
if not defined GAME_DIR if exist "%~dp0..\bin\Win64_Shipping_Client\Bannerlord.exe" for %%G in ("%~dp0..") do set "GAME_DIR=%%~fG"
if not defined GAME_DIR for %%G in ("D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord" "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord") do if exist "%%~G\bin\Win64_Shipping_Client\Bannerlord.exe" set "GAME_DIR=%%~G"
if not defined GAME_DIR for /f "tokens=2,*" %%A in ('reg query "HKCU\Software\Valve\Steam" /v SteamPath 2^>nul ^| findstr /I SteamPath') do if exist "%%~B\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\Bannerlord.exe" for %%G in ("%%~B\steamapps\common\Mount & Blade II Bannerlord") do set "GAME_DIR=%%~fG"
set "ARENA_VERSION=development"
if exist "%~dp0..\ARENA_VERSION" set /p ARENA_VERSION=<"%~dp0..\ARENA_VERSION"

if not exist "%GAME_DIR%\bin\Win64_Shipping_Client\Bannerlord.exe" (
  echo [ERROR] Bannerlord.exe was not found.
  echo Set BANNERLORD_GAME_DIR to your Mount ^& Blade II Bannerlord folder and run again.
  exit /b 1
)

if not exist "%~dp0..\Modules\DikozemieOnline\SubModule.xml" (
  echo [ERROR] Extract the complete archive before running this installer.
  exit /b 2
)

robocopy "%~dp0..\Modules\DikozemieOnline" "%GAME_DIR%\Modules\DikozemieOnline" /E /R:2 /W:1 /XD Source obj /XF *.csproj *.sln /NFL /NDL /NJH /NJS /NP >nul
if errorlevel 8 (
  echo [ERROR] Failed to install DikozemieOnline. Close Bannerlord and try again.
  exit /b 3
)
if exist "%GAME_DIR%\Modules\DikozemieOnline\GUI\Textures\calradia-multiplayer-map-detail.jpg" del /Q "%GAME_DIR%\Modules\DikozemieOnline\GUI\Textures\calradia-multiplayer-map-detail.jpg"

rem The Nexus package carries no files of the game: they come from this game.
if exist "%~dp0game_files.txt" (
  cscript.exe //nologo "%~dp0copy_game_files.js" "%~dp0game_files.txt" "%GAME_DIR%"
  if errorlevel 1 exit /b 7
)

cscript.exe //nologo "%~dp0prepare_singleplayer_catalog.js" "%GAME_DIR%\Modules\SandBoxCore\ModuleData\items" "%GAME_DIR%\Modules\Native\ModuleData\crafting_pieces.xml" "%GAME_DIR%\Modules\DikozemieOnline\ModuleData\singleplayer_catalog"
if errorlevel 1 (
  echo [ERROR] Failed to prepare the single-player item catalogue.
  exit /b 4
)
if not exist "%GAME_DIR%\Modules\DikozemieOnline\ModuleData\singleplayer_catalog\catalog-info.txt" (
  echo [ERROR] Prepared catalogue manifest is missing.
  exit /b 4
)
findstr /B /C:"EstimatedMultiplayerPrices=" "%GAME_DIR%\Modules\DikozemieOnline\ModuleData\singleplayer_catalog\catalog-info.txt" >nul
if errorlevel 1 (
  echo [ERROR] Prepared catalogue has no multiplayer prices.
  exit /b 4
)
findstr /B /C:"HistoricalPricePolicy=historical-penny-v2" "%GAME_DIR%\Modules\DikozemieOnline\ModuleData\singleplayer_catalog\catalog-info.txt" >nul
if errorlevel 1 (
  echo [ERROR] Prepared catalogue has no historical cultural price policy.
  exit /b 4
)
findstr /B /C:"AseraiPikePrice=" "%GAME_DIR%\Modules\DikozemieOnline\ModuleData\singleplayer_catalog\catalog-info.txt" >nul
if errorlevel 1 (
  echo [ERROR] Prepared catalogue did not audit the Aserai pike price.
  exit /b 4
)
cscript.exe //nologo "%~dp0audit_arena_prices.js" "%GAME_DIR%\Modules\DikozemieOnline\ModuleData\singleplayer_catalog"
if errorlevel 1 exit /b 4
findstr /B /C:"CreationEquipmentRosters=499" "%GAME_DIR%\Modules\DikozemieOnline\ModuleData\singleplayer_catalog\catalog-info.txt" >nul
if errorlevel 1 (
  echo [ERROR] Native character-creation equipment is incomplete.
  exit /b 4
)
findstr /B /C:"CreationBodyProperties=7" "%GAME_DIR%\Modules\DikozemieOnline\ModuleData\singleplayer_catalog\catalog-info.txt" >nul
if errorlevel 1 (
  echo [ERROR] Native culture body templates are incomplete.
  exit /b 4
)
findstr /B /C:"CreationLocalizationFiles=6" "%GAME_DIR%\Modules\DikozemieOnline\ModuleData\singleplayer_catalog\catalog-info.txt" >nul
if errorlevel 1 (
  echo [ERROR] Native character-creation localization is incomplete.
  exit /b 4
)
if not exist "%GAME_DIR%\Modules\DikozemieOnline\ModuleData\singleplayer_catalog\naval_arena_mp_crafting_pieces.xml" (
  echo [ERROR] Naval arena multiplayer crafting pieces are missing.
  exit /b 4
)
if not exist "%GAME_DIR%\Modules\DikozemieOnline\ModuleData\singleplayer_catalog\naval_arena_mpitems.xml" (
  echo [ERROR] Naval arena multiplayer items are missing.
  exit /b 4
)
if not exist "%GAME_DIR%\Modules\DikozemieOnline\ModuleData\singleplayer_catalog\naval_arena_characters.xml" (
  echo [ERROR] Naval arena multiplayer characters are missing.
  exit /b 4
)
findstr /B /C:"NavalArenaNativeItems=" "%GAME_DIR%\Modules\DikozemieOnline\ModuleData\singleplayer_catalog\catalog-info.txt" >nul
if errorlevel 1 (
  echo [ERROR] Naval arena multiplayer item audit is missing.
  exit /b 4
)
findstr /B /C:"NavalArenaNativeCharacters=" "%GAME_DIR%\Modules\DikozemieOnline\ModuleData\singleplayer_catalog\catalog-info.txt" >nul
if errorlevel 1 (
  echo [ERROR] Naval arena multiplayer character audit is missing.
  exit /b 4
)

rem The native Character Developer artwork belongs to SandBox and is not loaded
rem by Bannerlord's multiplayer module set. Reuse the player's installed game
rem assets without copying 137 MB into the mod package. The hard link occupies
rem no additional disk space and keeps the multiplayer startup independent from
rem the full SandBox campaign module.
if not exist "%GAME_DIR%\Modules\SandBox\GUI\SandBoxSpriteData.xml" (
  echo [ERROR] Native SandBox UI metadata was not found.
  exit /b 4
)
if not exist "%GAME_DIR%\Modules\DikozemieOnline\GUI\Brushes" mkdir "%GAME_DIR%\Modules\DikozemieOnline\GUI\Brushes"
copy /Y "%GAME_DIR%\Modules\SandBox\GUI\SandBoxSpriteData.xml" "%GAME_DIR%\Modules\DikozemieOnline\GUI\DikozemieSandBoxSpriteData.xml" >nul
copy /Y "%GAME_DIR%\Modules\SandBox\GUI\Brushes\CharacterDeveloper.xml" "%GAME_DIR%\Modules\DikozemieOnline\GUI\Brushes\CharacterDeveloper.xml" >nul
copy /Y "%GAME_DIR%\Modules\SandBox\GUI\Brushes\Inventory.xml" "%GAME_DIR%\Modules\DikozemieOnline\GUI\Brushes\Inventory.xml" >nul
if not exist "%GAME_DIR%\Modules\DikozemieOnline\AssetPackages" mkdir "%GAME_DIR%\Modules\DikozemieOnline\AssetPackages"
if exist "%GAME_DIR%\Modules\DikozemieOnline\AssetPackages\gauntlet_ui.tpac" del /Q "%GAME_DIR%\Modules\DikozemieOnline\AssetPackages\gauntlet_ui.tpac"
fsutil hardlink create "%GAME_DIR%\Modules\DikozemieOnline\AssetPackages\gauntlet_ui.tpac" "%GAME_DIR%\Modules\SandBox\AssetPackages\gauntlet_ui.tpac" >nul
if errorlevel 1 (
  echo [ERROR] Failed to link the native Character Developer artwork.
  exit /b 5
)

fc /B "%~dp0..\Modules\DikozemieOnline\bin\Win64_Shipping_Client\Dikozemie.Online.dll" "%GAME_DIR%\Modules\DikozemieOnline\bin\Win64_Shipping_Client\Dikozemie.Online.dll" >nul
if errorlevel 1 (
  echo [ERROR] Installed DLL verification failed.
  exit /b 6
)

>"%GAME_DIR%\Modules\DikozemieOnline\installed-build.txt" echo ArenaVersion=%ARENA_VERSION%
for /f "delims=" %%G in ('git -C "%~dp0.." rev-parse HEAD 2^>nul') do >>"%GAME_DIR%\Modules\DikozemieOnline\installed-build.txt" echo GitCommit=%%G

echo [OK] Arena %ARENA_VERSION% multiplayer client is installed.
echo Start it with Tools\launch_arena_client.cmd.
exit /b 0
