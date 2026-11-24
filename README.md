# Dikozemie Online (Wildlands)

A multiplayer conversion for **Mount & Blade II: Bannerlord** with the **War Sails** DLC.
It is one shared online Calradia with the Nord lands on it. Your hero travels by road and by ship, trades, walks the towns, and fights other players and bots in arenas and in naval battles on War Sails ships.

- Nexus Mods: https://www.nexusmods.com/mountandblade2bannerlord/mods/14953
- Video: https://youtu.be/nIpEnYapiUU
- Team: Boba billy & Th0mas. It is our entry in the TaleWorlds War Sails Modding Competition.

This repository holds the source code of `Dikozemie.Online.dll`, the only compiled file in the mod. It also holds every script of the Nexus archive. Anyone, and first of all the Nexus Mods staff, can check here what the mod does.

## What is here

| Path | Contents |
|---|---|
| `Source/Dikozemie.Online` | The C# project of `Dikozemie.Online.dll` |
| `Package/` | Every script and text file of the Nexus archive `Arena-Dikozemie-0.5.134-nexus.zip`, byte for byte, at the same paths |
| `Tools/build_nexus_package.py` | How the archive is packed |

The rest of the archive is data: XML, JSON, CSV/TSV, scenes and PNG/JPG images. It holds no other executable code.

Nexus file: *Dikozemie Online 0.5.134*, `Arena-Dikozemie-0.5.134-nexus.zip`
SHA-256 `fe54dbbc31119f0bad546dbb87c7de6efc72a854894fdde235ca2bfa17cbb2dd`
The DLL inside it, `Modules/DikozemieOnline/bin/Win64_Shipping_Client/Dikozemie.Online.dll`, has 1 842 176 bytes and SHA-256 `1acc33b6c5a191b8c08ec8809c3a633b4eb4b33a866d3b94bb6b27e9c377b77e`.

## Build

You need:
- Windows;
- .NET SDK 8 (we use 8.0.427);
- Bannerlord v1.5.7 with War Sails, installed through Steam. The project references the game's own assemblies, and those are not in this repository.

```
dotnet build Source/Dikozemie.Online/Dikozemie.Online.csproj -c Client -p:Platform=x64 "-p:BannerlordGameDir=C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord" -o out
```

The build gives `out/Dikozemie.Online.dll`, the same size as the DLL in the archive.

### Comparing with the DLL in the archive

The SDK stamps two things into the DLL: the id of our private commit in the version string, and the build folder in the debug-file path. To reproduce the archive's DLL, set both back:

```
dotnet build Source/Dikozemie.Online/Dikozemie.Online.csproj -c Client -p:Platform=x64 "-p:BannerlordGameDir=<game folder>" -p:SourceRevisionId=2922dda2cae78efeab99c8489377412e0fa08a7d "-p:PathMap=<full path of Source\Dikozemie.Online\ in your clone>=C:\work\_extracted\Thomas-ALL-2026-10-27\Source\Arena-Next\WildRealm\Bannerlord\DikozemieOnline\Modules\DikozemieOnline\Source\Dikozemie.Online\" -o out
```

After that, the DLL differs from the one in the archive in 72 bytes only. They are all build ids: the PE timestamp, the module id (MVID), and the debug file's id, timestamp and checksum. The code and the metadata are the same. We checked this with .NET SDK 8.0.427.

The same project also builds our dedicated server with `-c Server`, which needs the Bannerlord Dedicated Server. Code under `#if SERVER` and `#if !CLIENT` is not part of the client DLL.

The client DLL has no self-update. The Nexus package does not update itself.

## What the mod does outside the game

This is an online mod. It does not work without our servers.

**Network** (all in C#, no other programs):
- Battle servers: our Bannerlord custom servers, using the game's own multiplayer protocol over UDP 7210-7215.
- The trade and world server `https://136-234-5-140.sslip.io` (`ClientOnly/DikozemieEconomy.cs`, `ClientOnly/DikozemieDuelOfferController.cs`, `DikozemieArenaServerRegistration.cs`). It receives:
  - your in-game player id and name, and a random device key;
  - your hero's coins, items and looks;
  - your position on the map and in towns;
  - trade operations and tavern chat;
  - the reports you choose to send about other players.
- **Microphone:** this applies only when voice chat is ON in the game options and a microphone is connected. During battles the mod listens without a push-to-talk key. Your speech goes to your teammates through the game's own voice chat, and a shout triggers a battle cry (`DikozemieBattleCryBehavior.cs`). Turn voice chat off in the game options to switch this off.

**Files:**
- Hero data, logs and settings go to `%LOCALAPPDATA%\Dikozemie`.
- The local test battle (below) copies its config file into the Bannerlord Dedicated Server folder.
- Debug screenshots go to the temp folder, but only when the developer variable `DIKOZEMIE_NAVAL_CAPTURE=1` is set.

**Processes:**
- `ClientOnly/DikozemieGameRelaunch.cs` restarts the game in two cases: to switch between the multiplayer part and the single-player towns, and when the game was started without War Sails (`ClientOnly/DikozemieLaunchGuard.cs`). It writes a small `relaunch-<guid>.cmd` to `%LOCALAPPDATA%\Dikozemie\arena`. The script waits for the game to close, starts `Bannerlord.exe` with the mod's module list (or the module's `Launcher/launch_arena.js`), and then deletes itself.
- `ClientOnly/DikozemieArenaServerLauncher.cs` starts TaleWorlds' `DedicatedCustomServer.Starter.exe` on port 7210 for a local test battle. It does this only when you ask for it and only if the Bannerlord Dedicated Server is installed.

**Install scripts** (`Package/Tools`):
- `install_multiplayer_client.cmd` copies the module into the game folder.
- `copy_game_files.js` copies 28 scene and navmesh files that the mod shares with Bannerlord and War Sails from YOUR installed game, so the archive carries no game files. The list is in `game_files.txt`.
- `launch_arena_client.cmd` and the `Launcher` scripts start the game with the right modules.

None of them needs administrator rights.

## Use of AI

- Most of the C# code and tools were written with AI coding assistants (OpenAI Codex, Anthropic Claude Code) under the direction of the authors.
- The travel map is drawn by our own tool from sprites made with ChatGPT.
- The town, castle, port, village and culture paintings and the menu art were made with OpenAI image generation.
- There are no AI-generated 3D models, music, sounds or voices.

## Credits and license

Copyright (c) 2026 Boba billy & Th0mas. All rights reserved. The source is published so that it can be reviewed. It is not licensed for reuse.

Mount & Blade II: Bannerlord and War Sails belong to TaleWorlds Entertainment. This is a non-commercial fan modification, to be used only as part of the game, and is not affiliated with TaleWorlds. The custom multiplayer mode used TaleWorlds' BountyMP sample as a reference.
