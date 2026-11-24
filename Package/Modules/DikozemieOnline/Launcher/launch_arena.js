// Desktop shortcut target installed by ArenaSetup.exe into
// <game>\Modules\DikozemieOnline\Launcher. Starts Bannerlord multiplayer with
// the exact module set of Tools\launch_arena_client.cmd, without a console.
(function () {
    var shell = new ActiveXObject("WScript.Shell");
    var fileSystem = new ActiveXObject("Scripting.FileSystemObject");
    var launcherDir = fileSystem.GetParentFolderName(WScript.ScriptFullName);
    var moduleDir = fileSystem.GetParentFolderName(launcherDir);
    var gameDir = fileSystem.GetParentFolderName(
        fileSystem.GetParentFolderName(moduleDir));
    var binDir = fileSystem.BuildPath(gameDir, "bin\\Win64_Shipping_Client");
    var gameExe = fileSystem.BuildPath(binDir, "Bannerlord.exe");

    if (!fileSystem.FileExists(gameExe)) {
        shell.Popup(
            "Не найден Bannerlord.exe:\n" + gameExe +
            "\n\nПереустановите Арену через ArenaSetup.exe.",
            0, "Арена Дикоземья", 16);
        return;
    }
    if (!fileSystem.FileExists(fileSystem.BuildPath(gameDir, "Modules\\NavalDLC\\SubModule.xml"))) {
        shell.Popup(
            "Для Арены нужно дополнение War Sails (NavalDLC).",
            0, "Арена Дикоземья", 16);
        return;
    }

    // Acknowledge a stale safe-mode sentinel and sync the launcher's module
    // selection, exactly as launch_arena_client.cmd does.
    var prepare = fileSystem.BuildPath(launcherDir, "prepare_bannerlord_launch.js");
    if (fileSystem.FileExists(prepare)) {
        shell.Run("cscript.exe //nologo \"" + prepare + "\"", 0, true);
    }

    shell.Environment("PROCESS")("SteamAppId") = "261550";
    shell.CurrentDirectory = binDir;
    shell.Run(
        "\"" + gameExe + "\" /multiplayer " +
        "\"_MODULES_*Native*SandBoxCore*CustomBattle*Sandbox*StoryMode*NavalDLC*Multiplayer*DikozemieOnline*_MODULES_\"",
        1,
        false);
}());
