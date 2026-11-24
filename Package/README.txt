DIKOZEMIE ONLINE (WILDLANDS) 0.5.134 - multiplayer conversion for War Sails

A shared online Calradia for Mount & Blade II: Bannerlord. Your hero travels a
hand-drawn-style map of Calradia and the Nord lands by road and by ship, trades,
walks the towns, and fights other players and bots in arenas and in naval
battles on War Sails ships.

The interface is in Russian only for now.

REQUIREMENTS
- Mount & Blade II: Bannerlord v1.5.7 (Steam)
- War Sails (NavalDLC) v1.3.7
- Internet: outgoing UDP 7210-7215 and HTTPS

INSTALL
1. Close Bannerlord.
2. Unpack the archive to any folder (or right into the game folder).
3. Run Tools\install_multiplayer_client.cmd
   No administrator rights, Python or Node are needed. If the game is in an
   unusual place, set BANNERLORD_GAME_DIR to its folder first.
4. Start the mod with Tools\launch_arena_client.cmd

The archive contains no unmodified files of the game. The scenes and the ship
navmesh the mod shares with Bannerlord and War Sails are copied from YOUR
installed game by the install script (list: Tools\game_files.txt). Three battle
scenes and two lobby prefabs are modified versions of TaleWorlds scenes and
prefabs, made for use inside the game only.

UNINSTALL
Delete Modules\DikozemieOnline in the game folder. Hero data is kept in
%LOCALAPPDATA%\Dikozemie and can be deleted too.

WHAT THE MOD SENDS OVER THE INTERNET
It is an online mod and does not work without our servers:
- battle servers (Bannerlord multiplayer protocol, UDP 7210-7215);
- the trade and world server (https://136-234-5-140.sslip.io): your in-game
  player id and name, a random device key, your hero's coins, items, looks,
  position on the map and in towns, trade operations, tavern chat and the
  reports you choose to send about other players.
- MICROPHONE: if voice chat is ON in the game options and a microphone is
  connected, the mod listens to it during battles without a push-to-talk key:
  speech is sent to your teammates, a shout triggers a battle cry. Turn voice
  chat off in the game options to switch this off.
There is no self-update in this package and no payments of any kind.

USE OF AI
- Code: most of the C# code and tools was written with AI coding assistants
  (OpenAI Codex, Anthropic Claude Code) under the direction of the authors.
- Images: the travel map is drawn by our own tool from sprites made with
  ChatGPT; the town, castle, port, village and culture paintings and the menu
  art were made with OpenAI image generation.
- There are no AI-generated 3D models, music, sounds or voices: these come from
  your installed game.

CREDITS
- Team: Boba billy & Th0mas
- TaleWorlds Entertainment: Bannerlord, War Sails, the Modding Kit and the
  BountyMP sample that was used as a reference for a custom multiplayer mode.
- No third-party mods, models, music or sounds are included. No dependencies
  other than the official modules.

Non-commercial fan modification, to be used only as part of Mount & Blade II:
Bannerlord. Not affiliated with TaleWorlds Entertainment.
