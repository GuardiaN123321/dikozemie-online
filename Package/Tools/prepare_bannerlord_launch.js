// Bannerlord sets this sentinel to zero when it starts and restores it only
// after a normal exit.  A forced close therefore leaves the next launch
// blocked behind the game's one-time Safe Mode question.  We keep all user
// graphics settings intact and only acknowledge that stale sentinel.
(function () {
    var fileSystem = new ActiveXObject("Scripting.FileSystemObject");
    var shell = new ActiveXObject("WScript.Shell");
    var documents = shell.SpecialFolders("MyDocuments");
    var configPath = fileSystem.BuildPath(
        documents,
        "Mount and Blade II Bannerlord\\Configs\\engine_config.txt");

    if (!fileSystem.FileExists(configPath)) {
        return;
    }

    var reader = fileSystem.OpenTextFile(configPath, 1, false, -2);
    var current = reader.ReadAll();
    reader.Close();

    var updated = current.replace(
        /^safely_exited[ \t]*=[ \t]*0[ \t]*\r?$/m,
        "safely_exited = 1");
    if (updated === current) {
        return;
    }

    var writer = fileSystem.OpenTextFile(configPath, 2, false, -2);
    writer.Write(updated);
    writer.Close();
}());

// Direct command-line launches still pass through Bannerlord's module-change
// guard.  Keep the launcher's multiplayer selection in sync with the exact
// mixed module set used by launch_arena_client.cmd; otherwise Windows shows a
// blocking "Mod change detected" dialog before our code can render a frame.
(function () {
    var fileSystem = new ActiveXObject("Scripting.FileSystemObject");
    var shell = new ActiveXObject("WScript.Shell");
    var documents = shell.SpecialFolders("MyDocuments");
    var configPath = fileSystem.BuildPath(
        documents,
        "Mount and Blade II Bannerlord\\Configs\\LauncherData.xml");
    if (!fileSystem.FileExists(configPath)) {
        return;
    }

    var document = new ActiveXObject("Msxml2.DOMDocument.6.0");
    document.async = false;
    document.preserveWhiteSpace = true;
    if (!document.load(configPath)) {
        return;
    }

    var gameType = document.selectSingleNode("/UserData/GameType");
    var multiplayerMods = document.selectSingleNode(
        "/UserData/MultiplayerData/ModDatas");
    if (!gameType || !multiplayerMods) {
        return;
    }

    gameType.text = "Multiplayer";
    var required = [
        "Native",
        "SandBoxCore",
        "CustomBattle",
        "Sandbox",
        "StoryMode",
        "NavalDLC",
        "Multiplayer",
        "DikozemieOnline"
    ];

    // The guard compares the selected sequence, not merely membership. Clear
    // every prior multiplayer choice first so stale launcher selections cannot
    // precede, follow or interleave the command-line module list.
    var existingNodes = multiplayerMods.selectNodes("UserModData");
    for (var j = 0; j < existingNodes.length; j++) {
        var existingSelection = existingNodes.item(j).selectSingleNode(
            "IsSelected");
        if (existingSelection) {
            existingSelection.text = "false";
        }
    }

    for (var i = 0; i < required.length; i++) {
        var id = required[i];
        var node = multiplayerMods.selectSingleNode(
            "UserModData[Id='" + id + "']");
        if (!node) {
            var source = document.selectSingleNode(
                "/UserData/SingleplayerData/ModDatas/UserModData[Id='" +
                id + "']");
            node = source ? source.cloneNode(true) :
                document.createElement("UserModData");
            if (!source) {
                var idNode = document.createElement("Id");
                idNode.text = id;
                node.appendChild(idNode);
                var versionNode = document.createElement("LastKnownVersion");
                versionNode.text = id === "DikozemieOnline" ?
                    "v0.1.0.0" : "v1.5.7.125802";
                node.appendChild(versionNode);
                var selectedNode = document.createElement("IsSelected");
                node.appendChild(selectedNode);
            }
        }
        var selected = node.selectSingleNode("IsSelected");
        if (selected) {
            selected.text = "true";
        }

        // appendChild also moves an existing node to the end. Repeating this
        // in command-line order makes LauncherData's selected sequence exact.
        multiplayerMods.appendChild(node);
    }

    document.save(configPath);
}());
