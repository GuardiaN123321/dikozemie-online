// Copies the scenes and the ship navmesh the arena shares with the installed
// game from that game into the module. The Nexus package does not carry them
// (they belong to TaleWorlds); Tools\game_files.txt lists them, one per line:
//   <path inside Modules\DikozemieOnline>|<path inside the game folder>
// usage: cscript //nologo copy_game_files.js <list> <game folder>
var fso = new ActiveXObject("Scripting.FileSystemObject");

function makeFolder(path) {
    if (path === "" || fso.FolderExists(path)) {
        return;
    }
    makeFolder(fso.GetParentFolderName(path));
    fso.CreateFolder(path);
}

if (WScript.Arguments.length < 2) {
    WScript.StdErr.WriteLine("[ERROR] usage: copy_game_files.js <list> <game folder>");
    WScript.Quit(2);
}
var listPath = WScript.Arguments(0);
var game = WScript.Arguments(1);
var module = fso.BuildPath(fso.BuildPath(game, "Modules"), "DikozemieOnline");
var input = fso.OpenTextFile(listPath, 1);
var copied = 0;
while (!input.AtEndOfStream) {
    var line = input.ReadLine();
    if (line === "" || line.charAt(0) === "#") {
        continue;
    }
    var parts = line.split("|");
    var source = fso.BuildPath(game, parts[1]);
    var target = fso.BuildPath(module, parts[0]);
    if (!fso.FileExists(source)) {
        WScript.StdErr.WriteLine("[ERROR] The game file is missing: " + parts[1]);
        WScript.StdErr.WriteLine("Verify the integrity of the game files in Steam (War Sails is required).");
        input.Close();
        WScript.Quit(3);
    }
    makeFolder(fso.GetParentFolderName(target));
    if (fso.FileExists(target)) {
        fso.GetFile(target).Attributes = 0;
    }
    fso.CopyFile(source, target, true);
    fso.GetFile(target).Attributes = 0;
    copied++;
}
input.Close();
WScript.StdOut.WriteLine("[OK] " + copied + " game files reused.");
WScript.Quit(0);
